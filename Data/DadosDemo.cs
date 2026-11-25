using Microsoft.EntityFrameworkCore;

namespace SistemaEscolar.Data;

// Dados fictícios para demonstração (prints, vídeos). Só em Development:
//   dotnet run -- --demo           cria ~30 alunos, 4 turmas, professores, notas, frequência, cobranças e eventos
//   dotnet run -- --demo-limpar    remove tudo o que o --demo criou
// Tudo o que é criado fica marcado ([demo] nas observações, e-mails @demo.test) para a limpeza achar.
public static class DadosDemo
{
    const string Marca = "[demo]";
    const string AutorDemo = "Secretaria (demonstração)";
    const string SenhaDev = "Escola@123";

    static readonly string[] Femininos = { "Ana", "Beatriz", "Camila", "Daniela", "Eduarda", "Fernanda", "Gabriela", "Helena", "Isabela", "Julia", "Larissa", "Manuela", "Natalia", "Olivia", "Patricia" };
    static readonly string[] Masculinos = { "Arthur", "Bruno", "Caio", "Davi", "Enzo", "Felipe", "Gustavo", "Henrique", "Igor", "Joao", "Lucas", "Mateus", "Nicolas", "Otavio", "Pedro" };
    static readonly string[] Sobrenomes = { "Silva", "Souza", "Oliveira", "Santos", "Pereira", "Lima", "Costa", "Ribeiro", "Almeida", "Carvalho", "Gomes", "Martins", "Rocha", "Barbosa", "Moreira", "Nunes", "Araujo", "Cardoso", "Teixeira", "Mendes" };
    static readonly string[] Professores = { "Marcos Antunes", "Renata Figueiredo", "Paulo Vasconcelos", "Luciana Brandao", "Sergio Campos", "Tatiana Fontes" };

    public static string Executar(EscolaContext db, bool limpar)
    {
        if (limpar) return Limpar(db);
        if (db.Alunos.Any(a => a.Observacoes == Marca)) return "Os dados de demonstração já existem. Use --demo-limpar para remover.";

        var rnd = new Random(2026);
        var hoje = DateTime.Today;
        var ano = hoje.Year;
        int cpfSeq = 0;
        string Cpf()
        {
            var n = 700_000_000 + (++cpfSeq) * 7919;           // base fixa: não repete e não colide com cadastros reais
            var d = n.ToString("D9").Select(c => c - '0').ToList();
            for (int t = 9; t <= 10; t++)
            {
                int soma = 0;
                for (int i = 0; i < t; i++) soma += d[i] * (t + 1 - i);
                d.Add(soma * 10 % 11 % 10);
            }
            return Texto.FormatarCpf(string.Concat(d));
        }
        string Tel() => Texto.FormatarTelefone("319" + rnd.Next(10_000_000, 99_999_999));
        string Nasc(Serie s, out DateTime data)
        {
            var idade = s switch { Serie.Fund5 => 10, Serie.Fund8 => 13, Serie.Medio1 => 15, _ => 6 };
            data = new DateTime(ano - idade - rnd.Next(0, 2), rnd.Next(1, 13), rnd.Next(1, 28));
            return "";
        }

        // --- Professores (com login, senha de dev, sem troca obrigatória)
        var profs = new List<Professor>();
        foreach (var nome in Professores)
        {
            var email = nome.Split(' ')[0].ToLowerInvariant() + ".demo@demo.test";
            profs.Add(new Professor
            {
                Nome = nome, Email = email, Cpf = Cpf(), Telefone = Tel(), Formacao = "Licenciatura",
                DataNascimento = new DateTime(1975 + rnd.Next(0, 15), rnd.Next(1, 13), rnd.Next(1, 28)),
                Usuario = new Usuario { Nome = nome, Email = email, Perfil = Perfil.Professor, SenhaHash = Senha.Gerar(SenhaDev) }
            });
        }
        db.Professores.AddRange(profs);

        // --- Um funcionário por cargo (login com senha de dev, para mostrar cada visão)
        var funcionarios = new (string Nome, string Email, Cargo Cargo)[]
        {
            ("Juliana Prado", "secretaria.demo@demo.test", Cargo.Secretaria),
            ("Ricardo Menezes", "financeiro.demo@demo.test", Cargo.Financeiro),
            ("Adriana Lacerda", "coordenacao.demo@demo.test", Cargo.Coordenacao),
        };
        foreach (var f in funcionarios)
            db.Usuarios.Add(new Usuario { Nome = f.Nome, Email = f.Email, Perfil = Perfil.Funcionario, Cargo = f.Cargo, Areas = f.Cargo.Padrao(), SenhaHash = Senha.Gerar(SenhaDev) });
        db.SaveChanges();

        // --- Turmas do ano atual: série, letra, turno, quantos alunos
        var planos = new (Serie S, string L, Turno T, int N, decimal Mens)[]
        {
            (Serie.Fund5, "A", Turno.Manha, 8, 780m),
            (Serie.Fund5, "B", Turno.Tarde, 7, 780m),
            (Serie.Fund8, "A", Turno.Manha, 7, 850m),
            (Serie.Medio1, "A", Turno.Manha, 5, 1050m),
        };
        var contaresp = db.Usuarios.FirstOrDefault(u => u.Email == "responsavel@escola.test");
        var contaluno = db.Usuarios.FirstOrDefault(u => u.Email == "aluno@escola.test");
        int idx = 0, p = 0;
        var primeiro = true;
        var existentes = db.Turmas.Where(t => t.AnoLetivo == ano).Select(t => new { t.Serie, t.Letra }).ToList();

        foreach (var pl in planos)
        {
            if (existentes.Any(e => e.Serie == pl.S && e.Letra == pl.L)) continue; // não mexe em turma real
            var turma = new Turma { Serie = pl.S, Letra = pl.L, Turno = pl.T, AnoLetivo = ano };
            db.Turmas.Add(turma);
            db.SaveChanges();

            // disciplinas ativas da série, cada uma com um professor (revezando)
            var discs = db.Disciplinas.Where(d => d.Ativa && d.Series.Any(s => s.Serie == pl.S)).OrderBy(d => d.Nome).ToList();
            var atrib = discs.Select(d => (Disc: d, Prof: profs[p++ % profs.Count])).ToList();
            foreach (var x in atrib)
                db.Set<TurmaDisciplina>().Add(new TurmaDisciplina { TurmaId = turma.Id, DisciplinaId = x.Disc.Id, ProfessorId = x.Prof.Id });
            Mural(db, turma.Id, atrib.Select(x => (x.Disc.Id, x.Disc.Nome, x.Prof.Id)).ToList(), hoje);
            foreach (var d in discs)
                for (int b = 1; b <= TabelaNotas.Bimestres; b++)
                    db.Aulas.Add(new AulasDadas { TurmaId = turma.Id, DisciplinaId = d.Id, Bimestre = b, Aulas = 40 });
            db.SaveChanges();

            for (int i = 0; i < pl.N; i++, idx++)
            {
                var fem = idx % 2 == 0;
                var nome = $"{(fem ? Femininos : Masculinos)[rnd.Next(15)]} {Sobrenomes[rnd.Next(20)]} {Sobrenomes[rnd.Next(20)]}";
                var resp = new Responsavel
                {
                    Nome = $"{(rnd.Next(2) == 0 ? Femininos : Masculinos)[rnd.Next(15)]} {nome.Split(' ')[^1]}",
                    Cpf = Cpf(), Telefone = Tel(), Email = $"familia{idx + 1}@demo.test"
                };
                Nasc(pl.S, out var nascimento);
                // a primeira família usa as contas de teste, para o responsável/aluno de dev verem dados
                var aluno = new Aluno
                {
                    Nome = nome, DataNascimento = nascimento, Cpf = Cpf(), Observacoes = Marca, MesmoResponsavel = true,
                    Academico = resp, Financeiro = resp.Copia()
                };
                if (primeiro && contaresp != null)
                {
                    aluno.Academico.Email = aluno.Financeiro.Email = contaresp.Email;
                    aluno.UsuarioId = contaluno?.Id;
                    primeiro = false;
                }
                db.Alunos.Add(aluno);
                db.SaveChanges();

                var mat = new Matricula
                {
                    AlunoId = aluno.Id, Serie = pl.S, Turno = pl.T, AnoLetivo = ano, Status = StatusMatricula.Ativa,
                    DataMatricula = new DateTime(ano, 1, rnd.Next(5, 20)), ContratoAssinadoEm = new DateTime(ano, 1, rnd.Next(5, 20)),
                    TurmaId = turma.Id, Observacoes = null
                };
                db.Matriculas.Add(mat);
                db.SaveChanges();

                Cobrancas(db, mat, pl.Mens, idx, rnd, hoje);
                NotasEFrequencia(db, mat, discs.Select(d => d.Id).ToList(), rnd, idx);
            }
        }

        // --- Matrículas do ano seguinte, ainda pendentes (mostram o fluxo contrato + taxa)
        for (int k = 0; k < 3; k++, idx++)
        {
            var nome = $"{Femininos[rnd.Next(15)]} {Sobrenomes[rnd.Next(20)]} {Sobrenomes[rnd.Next(20)]}";
            var resp = new Responsavel { Nome = $"{Masculinos[rnd.Next(15)]} {nome.Split(' ')[^1]}", Cpf = Cpf(), Telefone = Tel(), Email = $"familia{idx + 1}@demo.test" };
            Nasc(Serie.Fund5, out var nascimento);
            var aluno = new Aluno { Nome = nome, DataNascimento = nascimento, Cpf = Cpf(), Observacoes = Marca, MesmoResponsavel = true, Academico = resp, Financeiro = resp.Copia() };
            db.Alunos.Add(aluno);
            db.SaveChanges();
            var mat = new Matricula
            {
                AlunoId = aluno.Id, Serie = Serie.Fund5, Turno = Turno.Manha, AnoLetivo = ano + 1, Status = StatusMatricula.Pendente,
                DataMatricula = hoje.AddDays(-rnd.Next(2, 12)),
                ContratoAssinadoEm = k == 0 ? hoje.AddDays(-3) : null   // 0: contrato ok, taxa aberta; 1: taxa paga, sem contrato; 2: nada ainda
            };
            db.Matriculas.Add(mat);
            db.SaveChanges();
            if (k <= 1)
            {
                db.Cobrancas.Add(new Cobranca
                {
                    MatriculaId = mat.Id, Tipo = TipoCobranca.Matricula, Numero = 0, Descricao = "Taxa de matrícula",
                    Vencimento = hoje.AddDays(k == 0 ? 7 : -2), Valor = 300m,
                    PagoEm = k == 1 ? hoje.AddDays(-2) : null, Forma = k == 1 ? FormaPagamento.Pix : null, ValorPago = k == 1 ? 300m : null
                });
                db.SaveChanges();
            }
        }

        // --- Calendário
        var ev = new (string T, TipoEvento Tp, DateTime I, DateTime? F)[]
        {
            ("Prova de Matemática", TipoEvento.Prova, hoje.AddDays(3), null),
            ("Prova de Português", TipoEvento.Prova, hoje.AddDays(5), null),
            ("Conselho de classe", TipoEvento.Reuniao, hoje.AddDays(9), null),
            ("Feira de Ciências", TipoEvento.Evento, hoje.AddDays(12), null),
            ("Recesso escolar", TipoEvento.Recesso, new DateTime(ano, 12, 22), new DateTime(ano + 1, 1, 5)),
            ("Entrega de boletins", TipoEvento.Reuniao, new DateTime(ano, 12, 10), null),
        };
        foreach (var e in ev)
            db.Eventos.Add(new Evento { Titulo = e.T, Tipo = e.Tp, Inicio = e.I.Date, Fim = e.F?.Date, Descricao = Marca });

        // Comunicados do mural (autor marcado para a limpeza achar)
        var comunicados = new (string T, string Tx, PublicoComunicado P, int Dias, bool Fix, int Atras)[]
        {
            ("Reunião de pais no sábado", "A reunião de pais e mestres será no sábado, às 9h, no pátio da escola. Contamos com a presença de todos.", PublicoComunicado.Responsaveis, 6, true, 1),
            ("Recesso de fim de ano", "As aulas terminam no dia 22 de dezembro e retornam em janeiro. A secretaria funciona normalmente até o dia 20.", PublicoComunicado.Todos, 25, false, 2),
            ("Entrega de boletins", "Os boletins do 4º bimestre serão entregues na secretaria a partir do dia 10 de dezembro.", PublicoComunicado.Responsaveis, 15, false, 3),
            ("Planejamento do próximo ano", "Reunião de planejamento com a coordenação na próxima semana. Tragam sugestões de projetos.", PublicoComunicado.Professores, 8, false, 1),
            ("Campanha do agasalho (encerrada)", "Obrigado a todos que participaram da campanha do agasalho.", PublicoComunicado.Todos, -3, false, 20),
        };
        foreach (var c in comunicados)
            db.Comunicados.Add(new Comunicado { Titulo = c.T, Texto = c.Tx, Publico = c.P, ValidoAte = hoje.AddDays(c.Dias).Date, Fixado = c.Fix, AutorNome = AutorDemo,
                CriadoEm = DateTime.SpecifyKind(hoje.AddDays(-c.Atras).AddHours(10), DateTimeKind.Unspecified) });
        db.SaveChanges();

        return $"Dados de demonstração criados: {db.Alunos.Count(a => a.Observacoes == Marca)} alunos, {profs.Count} professores. Logins (senha {SenhaDev}): professores <nome>.demo@demo.test e funcionários secretaria/financeiro/coordenacao.demo@demo.test. Para remover: dotnet run -- --demo-limpar";
    }

    // Algumas publicações da Sala de aula por turma (avisos, materiais e atividades, uma já encerrada)
    static void Mural(EscolaContext db, int turmaId, List<(int Id, string Nome, int ProfId)> discs, DateTime hoje)
    {
        DateTime Agora(int diasAtras, int hora) => DateTime.SpecifyKind(hoje.AddDays(-diasAtras).AddHours(hora), DateTimeKind.Unspecified);
        void Add(int i, TipoPublicacao t, string titulo, string? texto, string? link, DateTime? prazo, int diasAtras)
        {
            if (i >= discs.Count) return;
            db.Publicacoes.Add(new Publicacao { TurmaId = turmaId, DisciplinaId = discs[i].Id, ProfessorId = discs[i].ProfId, Tipo = t, Titulo = titulo, Texto = texto, Link = link, Prazo = prazo?.Date, CriadoEm = Agora(diasAtras, 9 + i) });
        }
        Add(0, TipoPublicacao.Aviso, "Prova na próxima semana", "Revisem os capítulos 4 e 5 do livro. Tragam dúvidas para a aula de revisão.", null, null, 1);
        Add(0, TipoPublicacao.Atividade, "Lista de exercícios 3", "Resolver os exercícios 1 a 10 da página 58 e trazer no caderno.", null, hoje.AddDays(6), 2);
        Add(1, TipoPublicacao.Material, "Resumo do conteúdo do bimestre", "Material de apoio para estudar em casa.", "https://pt.wikipedia.org/wiki/Educa%C3%A7%C3%A3o", null, 3);
        Add(1, TipoPublicacao.Atividade, "Pesquisa em dupla", "Pesquisar sobre o tema da aula e montar um cartaz.", null, hoje.AddDays(-2), 12);
        Add(2, TipoPublicacao.Atividade, "Relatório do experimento", "Entregar o relatório com introdução, método e conclusão.", null, hoje.AddDays(10), 4);
        Add(3, TipoPublicacao.Aviso, "Trazer material para a aula prática", "Na quinta-feira teremos aula prática. Não esqueçam o material.", null, null, 5);
    }

    // Taxa de matrícula + 12 mensalidades (vencem dia 10, de fevereiro a janeiro). Perfis de pagamento:
    // a maioria em dia; alguns com 1 mensalidade atrasada; poucos com 2-3; 2 com muitas.
    static void Cobrancas(EscolaContext db, Matricula m, decimal mens, int idx, Random rnd, DateTime hoje)
    {
        var k = idx * 11 % 27;   // espalha os perfis entre as turmas
        var atrasadas = k switch { >= 25 => 5, >= 22 => 3, >= 17 => 1, _ => 0 };
        var lista = new List<Cobranca>
        {
            new() { MatriculaId = m.Id, Tipo = TipoCobranca.Matricula, Numero = 0, Descricao = "Taxa de matrícula",
                    Vencimento = new DateTime(hoje.Year, 1, 15), Valor = 300m, PagoEm = new DateTime(hoje.Year, 1, 12), Forma = FormaPagamento.Pix, ValorPago = 300m }
        };
        var vencidas = new List<Cobranca>();
        for (int i = 1; i <= 12; i++)
        {
            var v = new DateTime(hoje.Year, 2, 10).AddMonths(i - 1);
            var c = new Cobranca { MatriculaId = m.Id, Tipo = TipoCobranca.Mensalidade, Numero = i, Descricao = $"Mensalidade {i:00}/12 ({v:MM/yyyy})", Vencimento = v, Valor = mens };
            lista.Add(c);
            if (v.Date <= hoje.Date) vencidas.Add(c);
        }
        foreach (var c in vencidas.Take(Math.Max(0, vencidas.Count - atrasadas)))
        {
            var atraso = rnd.Next(10) < 2 ? rnd.Next(1, 10) : -rnd.Next(0, 5);   // 20% pagam com atraso (com multa e juros)
            var data = c.Vencimento.AddDays(atraso);
            if (data.Date > hoje.Date) data = hoje.Date;
            c.PagoEm = data;
            c.Forma = (FormaPagamento)rnd.Next(0, 5);
            c.ValorPago = c.ValorAtualizado(data);
        }
        db.Cobrancas.AddRange(lista);
        db.SaveChanges();
    }

    // Notas dos 4 bimestres, recuperação para quem ficou entre as médias e faltas (poucas, exceto 2 alunos)
    static void NotasEFrequencia(EscolaContext db, Matricula m, List<int> discs, Random rnd, int idx)
    {
        var nivel = Math.Clamp(7.2 + (rnd.NextDouble() - 0.5) * 4.2, 3.0, 9.8);
        var faltoso = idx % 13 == 4;                       // alunos com muitas faltas
        foreach (var d in discs)
        {
            var v = new decimal?[TabelaNotas.Bimestres];
            for (int b = 1; b <= TabelaNotas.Bimestres; b++)
            {
                var nota = (decimal)Math.Clamp(nivel + (rnd.NextDouble() - 0.5) * 2.4, 0, 10);
                nota = Math.Round(nota, 1);
                v[b - 1] = nota;
                db.Notas.Add(new Nota { MatriculaId = m.Id, DisciplinaId = d, Bimestre = b, Valor = nota });
                var faltas = faltoso ? rnd.Next(8, 15) : (rnd.Next(10) < 7 ? rnd.Next(0, 3) : rnd.Next(2, 6));
                db.Frequencias.Add(new Frequencia { MatriculaId = m.Id, DisciplinaId = d, Bimestre = b, Faltas = faltas });
            }
            var media = Aprovacao.Media(v);
            if (media != null && media >= Aprovacao.MediaRecuperacao && media < Aprovacao.MediaMinima && rnd.Next(10) < 8)
                db.Notas.Add(new Nota { MatriculaId = m.Id, DisciplinaId = d, Bimestre = TabelaNotas.BimRecuperacao, Valor = Math.Round((decimal)(4 + rnd.NextDouble() * 4), 1) });
        }
        db.SaveChanges();
    }

    static string Limpar(EscolaContext db)
    {
        var alunos = db.Alunos.Where(a => a.Observacoes == Marca).Select(a => a.Id).ToList();
        var mats = db.Matriculas.Where(m => alunos.Contains(m.AlunoId!.Value)).ToList();
        var turmaIds = mats.Where(m => m.TurmaId != null).Select(m => m.TurmaId!.Value).Distinct().ToList();

        db.Matriculas.RemoveRange(mats);                    // notas, frequência e cobranças saem em cascata
        db.SaveChanges();
        db.Alunos.RemoveRange(db.Alunos.Where(a => alunos.Contains(a.Id)));
        db.SaveChanges();

        // turmas que ficaram vazias (só as que tinham alunos de demonstração)
        foreach (var t in db.Turmas.Where(t => turmaIds.Contains(t.Id)).ToList())
            if (!db.Matriculas.Any(m => m.TurmaId == t.Id))
            {
                db.Aulas.RemoveRange(db.Aulas.Where(a => a.TurmaId == t.Id));
                db.Turmas.Remove(t);                        // TurmaDisciplinas saem em cascata
            }
        db.SaveChanges();

        var profs = db.Professores.Where(p => p.Email.EndsWith("@demo.test")).Include(p => p.Usuario).ToList();
        db.Professores.RemoveRange(profs);
        db.SaveChanges();
        db.Usuarios.RemoveRange(profs.Where(p => p.Usuario != null).Select(p => p.Usuario!));
        db.Usuarios.RemoveRange(db.Usuarios.Where(u => u.Perfil == Perfil.Funcionario && u.Email.EndsWith("@demo.test")));
        db.Eventos.RemoveRange(db.Eventos.Where(e => e.Descricao == Marca));
        db.Comunicados.RemoveRange(db.Comunicados.Where(c => c.AutorNome == AutorDemo));
        db.SaveChanges();
        return $"Dados de demonstração removidos ({alunos.Count} alunos, {profs.Count} professores).";
    }
}
