# Sistema Escolar

Sistema web para escolas. Módulos: matrícula, alunos, professores, calendário acadêmico e notas.
Quatro perfis de acesso: responsáveis, funcionários, professores e alunos. Cada perfil vê só o necessário.

## Stack
- C# / ASP.NET Core (.NET 10), Razor Pages
- EF Core + PostgreSQL (Npgsql 10.x). Pacotes EF/Npgsql devem ficar na versão 10.x (a 11 exige .NET 11)
- Modo escuro desde o início (tema por variáveis CSS, `data-tema` no `<html>`)

## Desempenho (regra)
- Deve rodar bem em computadores antigos
- HTML renderizado no servidor; JavaScript mínimo, sem frameworks/bibliotecas (sem Bootstrap, sem jQuery)
- CSS leve em `wwwroot/css/site.css`; máscaras em `wwwroot/js/site.js` via `data-mask="cpf|tel|data|nome"`
- Validação é feita no servidor (sem validação client-side)

## Forma de trabalho
- Estilo vibe coding: testar, errar, corrigir. Código simples primeiro, sem se preocupar com arquitetura
- Um módulo por vez, passos pequenos, código que roda a cada etapa
- Quando algo quebrar, explicar a causa em poucas linhas
- Respostas sempre em português, diretas e curtas, mostrando o código completo dos arquivos alterados

## Como rodar
- `dotnet run` (http://localhost:5006). O segredo do banco vem do user-secrets e só vale em Development
- Conexão (não versionada). Em cada PC:
  `dotnet user-secrets set "ConnectionStrings:Escola" "Host=100.96.172.97;Port=5432;Database=claude_testes;Username=lovbin;Password=<senha>"`
  Em produção: variável `ConnectionStrings__Escola`. Nunca commitar a senha
- Banco criado com `EnsureCreated` (sem migrations). Ele NÃO altera tabelas existentes: mudou o modelo, é preciso apagar as tabelas ou adotar migrations (fazer antes de ter dados reais)
- Ao rodar a DLL direto (sem `dotnet run`) a porta é 5000 e o ambiente é Production, sem user-secrets: definir `ASPNETCORE_ENVIRONMENT=Development`

## Git
- Remoto: http://100.96.172.97:3002/lovbin/Sistema-Escolar.git (Gitea pessoal, via Tailscale), branch `main`
- `git pull` ao começar, commit e `git push` ao terminar. Autenticação com token do Gitea (nunca gravar o token em arquivo do repositório)

## Estado atual
### Módulo Matrícula (pronto e testado)
- CRUD em `Pages/Matriculas/`. Lista com busca (ILike) por aluno/responsável e filtro de status
- Modelo em `Data/Matricula.cs`; validadores (Nome, Cpf com dígitos verificadores, Telefone) em `Data/Validacoes.cs`
- Regras decididas:
  - Toda matrícula nasce **Pendente**, ignorando o que vier do formulário. Só a edição muda o status. No futuro a liberação virá do financeiro (pagamento + contrato assinado), manual por funcionário ou automática
  - Todos os campos obrigatórios, exceto Observações
  - Data no formato dd/MM/yyyy (cultura pt-BR). CPF `000.000.000-00` (11 dígitos válidos). Telefone `(31)9xxxx-xxxx` (celular, 11 dígitos). Nomes só com letras
  - Série em lista fixa (enum `Serie`: Maternal, Pré I/II, 1º–9º ano, 1ª–3ª série do médio), para padronizar relatórios
  - Ano letivo: só o ano atual e o seguinte (padrão: o seguinte)
  - Responsável acadêmico e financeiro (owned types `Academico` e `Financeiro`) agora ficam no `Aluno`. Se `MesmoResponsavel`, o financeiro é cópia do acadêmico, salva no servidor

### Login e perfis (testado no navegador)
- `Data/Usuario.cs`: entidade `Usuario`, enum `Perfil` (Responsavel, Funcionario, Professor, Aluno), hash PBKDF2 (`Senha`) e `UsuarioSeed`
- Cookie auth em `Program.cs`; política padrão exige login. `[AllowAnonymous]` em Login, Previa, Error. Pasta `/Matriculas` só para Funcionário (política `Funcionario`)
- A tabela `Usuarios` é criada por SQL (`CREATE TABLE IF NOT EXISTS`) no `UsuarioSeed.Preparar`, porque `EnsureCreated` não adiciona tabelas em banco existente
- Em Development cria 4 usuários `<perfil>@escola.test` (senha de dev em `UsuarioSeed`). Não vale em produção: criar usuários reais por outro meio
- Prévia TEMPORÁRIA e estática (dados fictícios): `/Previa/{perfil}` e `Pages/Shared/_Painel.cshtml` (usado também no `/` conforme o perfil). Remover/substituir conforme os módulos reais surgirem
- **Limite de tentativas** (`Data/Autenticacao.cs`, `LimiteLogin`): 5 falhas por e-mail ou 30 por IP bloqueiam por 15 min (mensagem com o tempo restante, nem confere a senha). Fica em memória: reiniciar o servidor zera. Vale também para errar a senha atual na troca
- **Trocar a própria senha**: `Pages/Conta/Senha` (menu "Alterar senha"; mínimo 8 caracteres com letra e número, diferente da atual). Senha provisória (professor novo, "Nova senha", acessos de aluno/responsável) marca `Usuario.DeveTrocarSenha`: no login o claim `trocar-senha` redireciona (middleware em `Program.cs`) para a troca até ela ser feita. Usuários de dev (`@escola.test`) não são forçados
- Cookie/claims criados por `Autenticacao.CriarPrincipal`

### Alunos e Matrícula (fluxo: matrícula primeiro, testado no navegador)
- REGRA ESSENCIAL: não existe cadastrar aluno sem matrícula. O aluno é criado **junto** com a matrícula, em `Pages/Matriculas/Create` (um formulário só: dados do aluno + responsáveis + série/turno/ano). Não há mais `Alunos/Create`
- Rematrícula: se o CPF já existe e o nome confere, reaproveita o aluno (atualiza data de nascimento e responsáveis) e cria a matrícula do novo ano. CPF de outro nome ou aluno já matriculado no ano: erro
- Excluir a última matrícula de um aluno exclui o aluno junto (aluno só existe com matrícula)
- `Pages/Alunos/` é só consulta (lista com busca) e correção dos dados (`Edit`), só Funcionário. Matrícula > Editar tem link para os dados do aluno; na edição da matrícula o aluno não muda
- Modelo: `Data/Aluno.cs` (nome, nascimento, CPF único, responsáveis acadêmico/financeiro, `MesmoResponsavel`) e `Matricula` (`AlunoId` FK Restrict, série, turno, ano, status, data, observações). Índice único `AlunoId + AnoLetivo`
- Partials compartilhados em `Pages/Shared`: `_DadosAluno` (aluno + responsáveis) e `_Responsavel`; `Pages/Matriculas/_Form` tem só os campos da matrícula. No Create os campos têm prefixo `Aluno.` e `Matricula.` (`Validar(ModelState, "prefixo")`)
- As tabelas `Alunos` e `Matriculas` são criadas por SQL em `Tabelas.Preparar`. Se achar a matrícula no formato antigo, migra e deixa `Matriculas_antiga` (backup, pode apagar)

### Módulo Professores (testado no navegador)
- `Data/Professor.cs` + `Pages/Professores/` (lista com busca e filtro ativo/inativo, novo, editar), só Funcionário. Tabela `Professores` criada por SQL em `TabelaProfessores.Preparar` (depois de `Usuarios`)
- Ao cadastrar, cria também o login (`Usuario`, perfil Professor) com o mesmo e-mail e uma **senha provisória** mostrada uma única vez (TempData). Editar nome/e-mail do professor atualiza o login
- Professor não é excluído: **Inativar** bloqueia também o login; **Nova senha** gera outra provisória. Não existe ainda tela para o usuário trocar a própria senha
- Todos os campos obrigatórios (nome, nascimento 18+, CPF único, telefone, e-mail único, formação)

### Módulo Disciplinas (testado no navegador)
- `Data/Disciplina.cs` + `Pages/Disciplinas/` (lista com busca e filtro, nova, editar, inativar), só Funcionário. Tabelas `Disciplinas` e `DisciplinaSeries` (disciplina × série, por enum `Serie`) criadas por SQL em `TabelasDisciplinas.Preparar`
- Cada disciplina marca as séries em que é oferecida (pelo menos uma). Não há exclusão, só inativar. Nome único, só letras
- Botão "Carregar disciplinas padrão" (só aparece com a lista vazia) cria um catálogo inicial (`TabelasDisciplinas.Padrao()`), editável. O banco de dev já tem as 16 padrão

### Módulo Turmas (testado no navegador)
- `Data/Turma.cs` + `Pages/Turmas/` (lista com filtro por ano/série, nova, detalhes), só Funcionário. Tabelas `Turmas` e `TurmaDisciplinas` (+ coluna `Matriculas.TurmaId`) criadas por SQL em `TabelasTurmas.Preparar`
- Turma = série + ano letivo + letra (A, B...) com um turno. Única por ano+série+letra. Nome exibido: "5º ano A"
- Enturmação pela matrícula (`Matricula.TurmaId`): só entra aluno com matrícula **Ativa**, mesma série, ano e turno, ainda sem turma. Remover tira o aluno da turma
- Cada disciplina ativa da série ganha um professor ativo (`TurmaDisciplina`); salvar em lote na tela de detalhes
- Regras ligadas à matrícula: matrícula que deixa de ser Ativa sai da turma sozinha; mudar série/ano/turno de aluno enturmado é bloqueado (remover da turma antes)
- Turma pode ser inativada; só exclui turma sem alunos (professores atribuídos saem junto)
- Sempre mapear tabela de entidade sem DbSet com `ToTable("...")` (o EF usa o nome da classe no singular, e a tabela criada por SQL é plural)

### Módulo Notas (testado no navegador)
- `Data/Nota.cs` (chave composta MatriculaId + DisciplinaId + Bimestre, valor 0 a 10 com 1 casa) e `TabelaNotas.Preparar` (SQL, depois de Turmas). 4 bimestres fixos (`TabelaNotas.Bimestres`)
- `Pages/Notas/` só Professor (política `Professor`): Index lista as disciplinas/turmas ativas atribuídas a ele (`TurmaDisciplina.ProfessorId`, ligado pelo `Professor.UsuarioId` = claim NameIdentifier); `Lancar` = notas de uma turma/disciplina/bimestre, só alunos com matrícula Ativa na turma
- Aceita vírgula ou ponto, arredonda para 1 casa; campo vazio apaga a nota; se um valor for inválido nada é salvo. Professor que não é o da disciplina: Acesso negado
- Falta: visão do funcionário sobre as notas

### Boletim e acessos (testado no navegador)
- `Pages/Boletim` para Aluno e Responsável (política `Boletim`, menu "Boletim"): disciplinas da turma × 4 bimestres + média das notas já lançadas (sem regra de aprovação ainda). Mostra a matrícula mais recente com turma
- Ligação: aluno por `Aluno.UsuarioId` (coluna criada em `TabelaNotas.Preparar`); responsável pelo **e-mail** (acadêmico ou financeiro) igual ao do login. Só vê os próprios alunos
- Funcionário cria os acessos em `Alunos/Edit` ("Acessos ao sistema"): aluno (informa e-mail) e responsável (e-mail já cadastrado); senha provisória mostrada uma vez
- Falta: visão do funcionário/professor, trocar a própria senha

### Módulo Calendário (testado no navegador)
- `Data/Evento.cs` (título, tipo, início, fim opcional, descrição; vale para a escola toda) e `TabelaEventos.Preparar` (SQL). Tipos: Evento, Feriado, Recesso, Prova, Reunião
- `Pages/Calendario/Index`: grade mensal (domingo a sábado) + lista do mês, navegação anterior/próximo, visível a todos os perfis logados (menu "Calendário"). `Pages/Calendario/Evento` (novo/editar/excluir) só Funcionário
- Erros de validação em `[BindProperty] Evento` precisam da chave com prefixo (`"Evento.Fim"`). Propriedades calculadas do modelo levam `[NotMapped, ValidateNever]` (senão o validador as lê e quebra com nulo)
- Falta: eventos por turma/série, ligar provas às turmas, início/fim dos bimestres

### Aprovação e média (testado no navegador)
- `Data/Aprovacao.cs`: limites **configuráveis** em `Pages/Configuracoes` (só Funcionário, menu "Configurações"). Ficam na tabela `Configuracoes` (linha única Id=1, criada por `TabelaConfiguracoes.Preparar`) e em propriedades estáticas carregadas na partida e atualizadas ao salvar (app de uma instância só). Média anual das 4 notas (arredonda 1 casa): **>= 6,0 Aprovado; 4,0 a 5,9 Recuperação; < 4,0 Reprovado**. Faltando nota em algum bimestre: "Em andamento"
- Situação geral do aluno: Em andamento se alguma disciplina está em andamento; senão Reprovado > Recuperação > Aprovado
- Aparece no `Boletim` (por disciplina e geral) e na tela `Notas/Lancar` (média anual e situação por aluno). Estilo `.sit-*` no CSS
- Recuperação deve ser menor que a aprovação (0 a 10, vírgula ou ponto). Mudar vale na hora para todos os boletins
- **Recuperação**: nota guardada em `Notas` como bimestre 5 (`TabelaNotas.BimRecuperacao`). Só quem tem as 4 notas e média entre a de recuperação e a de aprovação entra (aba "Recuperação" em `Notas/Lancar`). A média final depende do **modelo escolhido pela escola** em Configurações (`ModoRecuperacao`, coluna `Configuracoes.ModoRecuperacao`): 0 média das duas (padrão), 1 maior das duas, 2 recuperação substitui a média. Em todos precisa atingir a média de aprovação; senão Reprovado. Novo modelo = novo valor no enum + um caso em `Aprovacao.MediaFinal`
- **Frequência**: professor lança as **aulas dadas** por turma/disciplina/bimestre (`AulasDadas`) e as **faltas** por aluno (`Frequencias`) na mesma tela de notas. Faltas não passam das aulas dadas. Frequência % = 1 - faltas/aulas. Mínimo configurável (padrão 75%, coluna `Configuracoes.FrequenciaMinima`): frequência **geral** abaixo disso reprova o aluno (só depois de todas as disciplinas definidas). Boletim mostra Rec., Média final, Freq. por disciplina e geral
- Falta: abono/justificativa de faltas, frequência diária (chamada), regra de recuperação por bimestre
- Cuidado nos testes: se `dotnet run` falhar em silêncio, pode haver servidor antigo na porta 5006 (`ss -ltnp | grep 5006`): encerrar pelo PID

### Módulo Financeiro (testado no navegador)
- `Data/Financeiro.cs`: `Cobranca` (tipo Matrícula/Mensalidade, número, vencimento, valor, `PagoEm`, forma), `TabelaFinanceiro.Preparar` (SQL; também cria `Matriculas.ContratoAssinadoEm`) e `RegrasFinanceiro` (ler valor "1.250,50", ler data dd/MM/yyyy, `TentarLiberar`). A classe de regras NÃO pode se chamar `Financeiro`: o namespace `Pages.Financeiro` a esconde
- `Pages/Financeiro/` só Funcionário: `Index` (lista com busca e filtros: em atraso, pendente, sem contrato, sem cobranças; mostra em aberto e atrasado) e `Detalhe/{matrículaId}`: registrar contrato assinado, gerar cobranças (taxa opcional + 1 a 12 mensalidades, vencimento mensal a partir da 1ª), dar baixa (data não futura + forma: Pix, Dinheiro, Cartão, Boleto, Transferência), desfazer pagamento, apagar cobranças em aberto
- **Liberação da matrícula**: Pendente vira Ativa sozinha quando o contrato está assinado **e** a taxa de matrícula está paga (`TentarLiberar`, chamada após baixa e após registrar contrato). Também há "Liberar manualmente". Desfazer pagamento não volta o status
- Matrícula com pagamento registrado não pode ser excluída (cancelar em vez disso); sem pagamento, excluir leva as cobranças junto
- `Pages/Mensalidades` (política `Responsavel`, menu "Mensalidades"): responsável vê as cobranças dos alunos ligados ao e-mail dele (acadêmico ou financeiro), só leitura
- Atrasada = não paga e vencida antes de hoje. **Multa e juros** (`Cobranca.Encargos`): multa única de X% do valor + juros simples de Y% ao mês proporcionais aos dias (padrão 2% e 1%, configuráveis em Configurações, colunas `Configuracoes.MultaPercentual/JurosMensal`). Calculados na hora para cobranças em aberto; na baixa o valor recebido é gravado em `Cobranca.ValorPago` (histórico não muda se as taxas mudarem). Checkbox "Isentar multa e juros" na baixa. Sem desconto, sem boleto/Pix real (baixa é manual)
- Falta: descontos/bolsas, recibo para imprimir, relatório de inadimplência, integração com pagamento online

### Pendências conhecidas
- Alunos antigos sem matrícula (criados no fluxo anterior) ficam órfãos: matricular de novo pelo CPF ou apagar no banco
- Senha: o funcionário gera a provisória e o usuário troca no primeiro acesso. Não há "esqueci minha senha" por e-mail (só o funcionário gera outra)
- Próximos passos possíveis: Sala de aula real (professor/aluno), migrations, testes automatizados, publicar no GitHub

## Cuidados
- `psql` instalado. Para rodar SQL com a conexão do user-secrets sem expor a senha: ler `dotnet user-secrets list` e passar Host/Port/Username/Database por parâmetro e a senha por `PGPASSWORD`
- Rodar servidores de teste em background: guardar o PID e encerrar com `kill <pid>`. Não usar `pkill -f` com o nome da DLL, pois mata o próprio shell
- Dados de teste criados no banco compartilhado devem ser apagados ao final
- Fim do dia: quando o usuário rodar `git commit` e `git push` dizendo que finalizamos por hoje, apagar do banco os dados de exemplo (matrículas de teste e usuários `@escola.test`). Em Development os usuários de teste são recriados na próxima execução
- Cada perfil só vê o que lhe cabe no menu (Funcionário: Matrículas; Professor/Aluno: Sala de aula). Página fora do perfil: Acesso negado
