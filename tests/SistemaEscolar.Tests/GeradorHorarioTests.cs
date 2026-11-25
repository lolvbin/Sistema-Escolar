using SistemaEscolar.Data;

namespace SistemaEscolar.Tests;

public class GeradorHorarioTests
{
    // Turno da manhã: aulas nas ordens 0, 1, (2 = intervalo), 3, 4, 5 → ids 1..5. Tarde: ids 11..15.
    static readonly List<SlotAula> Manha = new() { new(1, Turno.Manha, 0), new(2, Turno.Manha, 1), new(3, Turno.Manha, 3), new(4, Turno.Manha, 4), new(5, Turno.Manha, 5) };
    static readonly List<SlotAula> Tarde = new() { new(11, Turno.Tarde, 0), new(12, Turno.Tarde, 1), new(13, Turno.Tarde, 3), new(14, Turno.Tarde, 4), new(15, Turno.Tarde, 5) };

    static EntradaGerador Nova(int maxSeg = 3, int maxDia = 2, int semente = 1)
    {
        var e = new EntradaGerador { MaxSeguidas = maxSeg, MaxPorDia = maxDia, Semente = semente };
        e.Slots.AddRange(Manha); e.Slots.AddRange(Tarde);
        return e;
    }
    static void Turma(EntradaGerador e, int id, bool tarde = false) => e.SlotsDaTurma[id] = (tarde ? Tarde : Manha).Select(s => s.Id).ToList();
    static void Pede(EntradaGerador e, int turma, int disc, int? prof, int carga) => e.Pedidos.Add(new PedidoAula(turma, disc, prof, carga));

    static List<Violacao> Confere(EntradaGerador e, ResultadoGerador r) =>
        GeradorHorario.Verificar(r.Aulas, e.Slots, e.Indisponiveis, e.Ocupados, e.MaxSeguidas, e.MaxPorDia);

    // ---------- casos simples ----------

    [Fact] public void Turma_cheia_cabe_e_respeita_a_carga_de_cada_disciplina()
    {
        var e = Nova(); Turma(e, 1);
        for (int d = 1; d <= 5; d++) Pede(e, 1, d, d, 5);
        var r = GeradorHorario.Gerar(e);
        Assert.True(r.Completo);
        Assert.Equal(25, r.Aulas.Count);
        for (int d = 1; d <= 5; d++) Assert.Equal(5, r.Aulas.Count(a => a.DisciplinaId == d));
        Assert.Empty(Confere(e, r));
    }

    [Fact] public void Carga_menor_que_os_horarios_deixa_vazios_no_fim_do_dia()
    {
        var e = Nova(); Turma(e, 1);
        Pede(e, 1, 1, 1, 5); Pede(e, 1, 2, 2, 5); Pede(e, 1, 3, 3, 5);   // 15 aulas em 25 horários
        var r = GeradorHorario.Gerar(e);
        Assert.True(r.Completo);
        Assert.Equal(15, r.Aulas.Count);
        Assert.Empty(Confere(e, r));
        // sem buracos: em cada dia as aulas ocupam os primeiros horários
        foreach (var dia in r.Aulas.GroupBy(a => a.Dia))
        {
            var ids = dia.Select(a => a.SlotId).OrderBy(i => i).ToList();
            Assert.Equal(Enumerable.Range(1, ids.Count).ToList(), ids);
        }
    }

    [Fact] public void Disciplina_sem_professor_tambem_e_colocada()
    {
        var e = Nova(); Turma(e, 1);
        Pede(e, 1, 1, null, 5); Pede(e, 1, 2, 2, 5);
        var r = GeradorHorario.Gerar(e);
        Assert.True(r.Completo);
        Assert.Equal(5, r.Aulas.Count(a => a.DisciplinaId == 1));
    }

    [Fact] public void Carga_zero_nao_gera_aula()
    {
        var e = Nova(); Turma(e, 1);
        Pede(e, 1, 1, 1, 0); Pede(e, 1, 2, 2, 3);
        var r = GeradorHorario.Gerar(e);
        Assert.Equal(3, r.Aulas.Count);
        Assert.DoesNotContain(r.Aulas, a => a.DisciplinaId == 1);
    }

    [Fact] public void Mesma_semente_mesmo_resultado()
    {
        EntradaGerador Monta()
        {
            var e = Nova(semente: 42); Turma(e, 1); Turma(e, 2);
            for (int d = 1; d <= 5; d++) { Pede(e, 1, d, d, 5); Pede(e, 2, d, d, 5); }
            return e;
        }
        var a = GeradorHorario.Gerar(Monta()).Aulas;
        var b = GeradorHorario.Gerar(Monta()).Aulas;
        Assert.Equal(a.Select(x => (x.TurmaId, x.Dia, x.SlotId, x.DisciplinaId)), b.Select(x => (x.TurmaId, x.Dia, x.SlotId, x.DisciplinaId)));
    }

    // ---------- regras duras ----------

    [Fact] public void Professor_indisponivel_nao_recebe_aula_naquele_horario()
    {
        var e = Nova(); Turma(e, 1);
        for (int d = 1; d <= 5; d++) Pede(e, 1, d, d, 5);
        foreach (var s in Manha) e.Indisponiveis.Add((1, 1, s.Id));   // professor 1 não vai na segunda
        var r = GeradorHorario.Gerar(e);
        Assert.True(r.Completo);
        Assert.DoesNotContain(r.Aulas, a => a.ProfessorId == 1 && a.Dia == 1);
        Assert.Empty(Confere(e, r));
    }

    [Fact] public void Professor_compartilhado_entre_turmas_nunca_fica_em_dois_lugares()
    {
        var e = Nova(); Turma(e, 1); Turma(e, 2); Turma(e, 3);
        for (int t = 1; t <= 3; t++)
            for (int d = 1; d <= 5; d++) Pede(e, t, d, d, 5);   // 5 professores dão aula nas 3 turmas ao mesmo tempo possível
        var r = GeradorHorario.Gerar(e);
        Assert.True(r.Completo);
        Assert.Equal(75, r.Aulas.Count);
        Assert.Empty(Confere(e, r));
    }

    [Fact] public void Limite_de_aulas_seguidas_do_professor_e_respeitado()
    {
        var e = Nova(maxSeg: 2); Turma(e, 1);
        // professor 1 dá 15 das 25 aulas: sem limite ele pegaria 3 seguidas fácil
        Pede(e, 1, 1, 1, 5); Pede(e, 1, 2, 1, 5); Pede(e, 1, 3, 1, 5); Pede(e, 1, 4, 2, 5); Pede(e, 1, 5, 3, 5);
        var r = GeradorHorario.Gerar(e);
        Assert.True(r.Completo);
        Assert.Empty(Confere(e, r));
        // confere à mão: nunca 3 horários seguidos (ids 1,2 | intervalo | 3,4,5)
        foreach (var dia in r.Aulas.Where(a => a.ProfessorId == 1).GroupBy(a => a.Dia))
        {
            var ordem = dia.Select(a => Manha.First(s => s.Id == a.SlotId).Ordem).OrderBy(o => o).ToList();
            int run = 1, max = 1;
            for (int i = 1; i < ordem.Count; i++) { run = ordem[i] == ordem[i - 1] + 1 ? run + 1 : 1; max = Math.Max(max, run); }
            Assert.True(max <= 2, $"dia {dia.Key}: {max} seguidas");
        }
    }

    [Fact] public void Intervalo_quebra_a_sequencia_de_aulas_seguidas()
    {
        // limite de 2 seguidas: o professor dá 4 aulas por dia (2 antes do intervalo e 2 depois) em duas disciplinas
        var e = Nova(maxSeg: 2, maxDia: 2); Turma(e, 1);
        Pede(e, 1, 1, 1, 10); Pede(e, 1, 2, 1, 10);
        var r = GeradorHorario.Gerar(e);
        Assert.True(r.Completo);
        Assert.Empty(Confere(e, r));
        foreach (var dia in r.Aulas.GroupBy(a => a.Dia)) Assert.Equal(4, dia.Count());
    }

    [Fact] public void Seguidas_conta_aulas_do_professor_em_turmas_fora_da_geracao()
    {
        var e = Nova(maxSeg: 2); Turma(e, 1);
        // professor 1 já tem aula fixa na segunda nos horários 1 e 2 (turma que não será gerada): na segunda ele não pode pegar o 3º... mas o intervalo quebra
        e.Ocupados.Add((1, 1, 1)); e.Ocupados.Add((1, 1, 2));
        Pede(e, 1, 1, 1, 8);
        var r = GeradorHorario.Gerar(e);
        Assert.True(r.Completo);
        Assert.Empty(Confere(e, r));
        Assert.DoesNotContain(r.Aulas, a => a.ProfessorId == 1 && a.Dia == 1 && (a.SlotId == 1 || a.SlotId == 2));
    }

    [Fact] public void Ocupado_em_outra_turma_fixa_nao_e_usado()
    {
        var e = Nova(); Turma(e, 1);
        for (int s = 1; s <= 5; s++) e.Ocupados.Add((1, 2, s));   // professor 1 ocupado a terça inteira
        Pede(e, 1, 1, 1, 5);
        var r = GeradorHorario.Gerar(e);
        Assert.True(r.Completo);
        Assert.DoesNotContain(r.Aulas, a => a.Dia == 2);
    }

    [Fact] public void Disciplina_respeita_o_limite_por_dia_e_aula_dupla_fica_colada()
    {
        var e = Nova(maxDia: 2); Turma(e, 1);
        Pede(e, 1, 1, 1, 7);   // 7 aulas em 5 dias: obrigatoriamente 2 dias com aula dupla
        Pede(e, 1, 2, 2, 5);
        var r = GeradorHorario.Gerar(e);
        Assert.True(r.Completo);
        Assert.Empty(Confere(e, r));
        foreach (var d in r.Aulas.Where(a => a.DisciplinaId == 1).GroupBy(a => a.Dia)) Assert.True(d.Count() <= 2);
    }

    [Fact] public void Disciplina_dupla_nao_atravessa_o_intervalo()
    {
        var e = Nova(maxDia: 2); Turma(e, 1);
        Pede(e, 1, 1, 1, 10);   // 2 por dia em 5 dias
        var r = GeradorHorario.Gerar(e);
        Assert.True(r.Completo);
        foreach (var d in r.Aulas.GroupBy(a => a.Dia))
        {
            var ids = d.Select(a => a.SlotId).OrderBy(i => i).ToList();
            Assert.False(ids.SequenceEqual(new[] { 2, 3 }), "ids 2 e 3 estão separados pelo intervalo");
        }
        Assert.Empty(Confere(e, r));
    }

    // ---------- o que não cabe ----------

    [Fact] public void Professor_indisponivel_o_tempo_todo_nao_trava_as_outras_disciplinas()
    {
        var e = Nova(); Turma(e, 1);
        Pede(e, 1, 1, 1, 5); Pede(e, 1, 2, 2, 5);
        for (int d = 1; d <= 5; d++) foreach (var s in Manha) e.Indisponiveis.Add((1, d, s.Id));
        var r = GeradorHorario.Gerar(e);
        Assert.False(r.Completo);
        Assert.Equal(5, r.Aulas.Count(a => a.DisciplinaId == 2));   // a outra disciplina foi colocada
        var nc = Assert.Single(r.NaoColocadas);
        Assert.Equal(1, nc.DisciplinaId); Assert.Equal(5, nc.Faltam);
        Assert.Contains("indisponível", nc.Motivo);
        Assert.Contains(r.Problemas, p => p.Tipo == TipoProblema.ProfessorExcede && p.ProfessorId == 1);
        Assert.Empty(Confere(e, r));
    }

    [Fact] public void Mais_aulas_que_horarios_e_avisado_e_o_resto_e_colocado()
    {
        var e = Nova(); Turma(e, 1);
        for (int d = 1; d <= 6; d++) Pede(e, 1, d, d, 5);   // 30 aulas para 25 horários
        var r = GeradorHorario.Gerar(e);
        Assert.False(r.Completo);
        Assert.Contains(r.Problemas, p => p.Tipo == TipoProblema.TurmaExcede && p.Necessarias == 30 && p.Disponiveis == 25);
        Assert.Equal(25, r.Aulas.Count);
        Assert.Equal(5, r.NaoColocadas.Sum(n => n.Faltam));
        Assert.Empty(Confere(e, r));
    }

    [Fact] public void Disciplina_com_carga_acima_do_limite_por_dia_e_avisada()
    {
        var e = Nova(maxDia: 1); Turma(e, 1);
        Pede(e, 1, 1, 1, 7);   // máximo 1 por dia → 5
        var r = GeradorHorario.Gerar(e);
        Assert.Contains(r.Problemas, p => p.Tipo == TipoProblema.DisciplinaExcede && p.Necessarias == 7 && p.Disponiveis == 5);
        Assert.Equal(5, r.Aulas.Count);
        Assert.Equal(2, r.NaoColocadas.Single().Faltam);
    }

    [Fact] public void Professor_com_menos_horarios_livres_que_aulas_perde_so_o_excesso_e_o_resto_fecha()
    {
        var e = Nova(); Turma(e, 1);
        Pede(e, 1, 1, 1, 5); Pede(e, 1, 2, 1, 5);   // professor 1: 10 aulas
        Pede(e, 1, 3, 2, 5); Pede(e, 1, 4, 3, 5); Pede(e, 1, 5, 4, 5);
        // só 7 horários livres para o professor 1 (segunda inteira + 2 da terça)
        for (int dia = 1; dia <= 5; dia++) foreach (var sl in Manha) e.Indisponiveis.Add((1, dia, sl.Id));
        e.Indisponiveis.RemoveWhere(x => x.Item1 == 1 && (x.Item2 == 1 || (x.Item2 == 2 && x.Item3 <= 2)));
        var r = GeradorHorario.Gerar(e);
        Assert.Empty(Confere(e, r));
        int doProf = r.Aulas.Count(a => a.ProfessorId == 1);
        Assert.InRange(doProf, 6, 7);   // na segunda cabem 4 (2 por disciplina) e na terça 2 coladas; o 7º horário só serve com aula tripla
        Assert.Equal(10 - doProf, r.NaoColocadas.Where(n => n.DisciplinaId <= 2).Sum(n => n.Faltam));
        Assert.Contains(r.NaoColocadas, n => n.Motivo.Contains("só tem 7") || n.Motivo.Contains("só cabem"));
        Assert.Equal(15, r.Aulas.Count(a => a.DisciplinaId >= 3));   // as outras disciplinas fecharam inteiras
    }

    [Fact] public void Motivo_do_limite_por_dia_e_explicado()
    {
        var e = Nova(maxDia: 1); Turma(e, 1);
        Pede(e, 1, 1, 1, 7);
        var r = GeradorHorario.Gerar(e);
        Assert.Contains("limite de 1 aula(s)", Assert.Single(r.NaoColocadas).Motivo);
    }

    [Fact] public void Turma_sem_horarios_cadastrados_e_informada()
    {
        var e = Nova(); e.SlotsDaTurma[1] = new List<int>();
        Pede(e, 1, 1, 1, 3);
        var r = GeradorHorario.Gerar(e);
        Assert.Empty(r.Aulas);
        Assert.Contains("horários", Assert.Single(r.NaoColocadas).Motivo);
    }

    [Fact] public void Sem_pedidos_devolve_vazio_e_completo()
    {
        var r = GeradorHorario.Gerar(Nova());
        Assert.True(r.Completo); Assert.Empty(r.Aulas);
    }

    // ---------- turnos diferentes ----------

    [Fact] public void Professor_em_turnos_diferentes_nao_gera_conflito_nem_seguidas()
    {
        var e = Nova(maxSeg: 3); Turma(e, 1); Turma(e, 2, tarde: true);
        Pede(e, 1, 1, 1, 10); Pede(e, 1, 2, 2, 5); Pede(e, 2, 1, 1, 10); Pede(e, 2, 2, 2, 5);   // professor 1 dá 10 de manhã e 10 à tarde
        var r = GeradorHorario.Gerar(e);
        Assert.True(r.Completo);
        Assert.Empty(Confere(e, r));
    }

    // ---------- lacunas (horário vago no meio do dia) ----------

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void Carga_menor_que_os_horarios_nao_deixa_lacuna(int semente)
    {
        var e = Nova(semente: semente); Turma(e, 1); Turma(e, 2);
        // 17 aulas em 25 horários; professores compartilhados entre as duas turmas
        Pede(e, 1, 1, 1, 4); Pede(e, 1, 2, 2, 4); Pede(e, 1, 3, 3, 5); Pede(e, 1, 4, 4, 4);
        Pede(e, 2, 1, 2, 4); Pede(e, 2, 2, 1, 4); Pede(e, 2, 3, 4, 5); Pede(e, 2, 4, 3, 4);
        var r = GeradorHorario.Gerar(e);
        Assert.True(r.Completo);
        Assert.Equal(34, r.Aulas.Count);
        Assert.Empty(GeradorHorario.Lacunas(r.Aulas, e.Slots));
        Assert.Empty(Confere(e, r));
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void Professor_indisponivel_no_fim_do_dia_encurta_o_dia_sem_lacuna(int semente)
    {
        var e = Nova(semente: semente); Turma(e, 1);
        for (int d = 1; d <= 5; d++) Pede(e, 1, d, d, 4);   // 20 aulas, 25 horários
        // o professor 5 não pode nos dois últimos horários de quarta a sexta; os dias dele têm de acabar antes
        foreach (var dia in new[] { 3, 4, 5 }) { e.Indisponiveis.Add((5, dia, 4)); e.Indisponiveis.Add((5, dia, 5)); }
        var r = GeradorHorario.Gerar(e);
        Assert.True(r.Completo);
        Assert.Empty(GeradorHorario.Lacunas(r.Aulas, e.Slots));
        Assert.Empty(Confere(e, r));
    }

    [Fact] public void Verificador_de_lacunas()
    {
        var slots = Manha;   // posições 0..4 = ids 1..5 (intervalo entre 2 e 3 não conta)
        AulaProposta A(int dia, int slot) => new(1, dia, slot, 1, null);
        Assert.Empty(GeradorHorario.Lacunas(new[] { A(1, 1), A(1, 2), A(1, 3) }, slots));          // 1º, 2º e 3º: sem lacuna (o intervalo não é horário)
        Assert.Equal(new[] { (1, 1) }, GeradorHorario.Lacunas(new[] { A(1, 1), A(1, 3) }, slots));  // 2º horário vago no meio
        Assert.Equal(new[] { (1, 2) }, GeradorHorario.Lacunas(new[] { A(2, 2) }, slots));            // dia começa no 2º horário
        Assert.Empty(GeradorHorario.Lacunas(new[] { A(1, 1), A(3, 1), A(3, 2) }, slots));            // dias diferentes, cada um colado ao início
    }

    // ---------- verificador ----------

    [Fact] public void Verificador_pega_cada_tipo_de_violacao()
    {
        var slots = Manha;
        var vazio = new HashSet<(int, int, int)>();
        // professor duplo
        var v1 = GeradorHorario.Verificar(new[] { new AulaProposta(1, 1, 1, 1, 7), new AulaProposta(2, 1, 1, 2, 7) }, slots, vazio, vazio, 3, 2);
        Assert.Contains(v1, v => v.Tipo == TipoViolacao.ProfessorDuplo);
        // indisponível
        var v2 = GeradorHorario.Verificar(new[] { new AulaProposta(1, 1, 1, 1, 7) }, slots, new HashSet<(int, int, int)> { (7, 1, 1) }, vazio, 3, 2);
        Assert.Contains(v2, v => v.Tipo == TipoViolacao.Indisponivel);
        // seguidas: ordens 3,4,5 com limite 2
        var v3 = GeradorHorario.Verificar(new[] { new AulaProposta(1, 1, 3, 1, 7), new AulaProposta(1, 1, 4, 2, 7), new AulaProposta(1, 1, 5, 3, 7) }, slots, vazio, vazio, 2, 2);
        Assert.Contains(v3, v => v.Tipo == TipoViolacao.Seguidas);
        // seguidas, mas o intervalo separa (ordens 1 e 3)
        var v4 = GeradorHorario.Verificar(new[] { new AulaProposta(1, 1, 1, 1, 7), new AulaProposta(1, 1, 2, 2, 7), new AulaProposta(1, 1, 3, 3, 7) }, slots, vazio, vazio, 2, 2);
        Assert.DoesNotContain(v4, v => v.Tipo == TipoViolacao.Seguidas);
        // disciplina 3 vezes no dia / separada
        var v5 = GeradorHorario.Verificar(new[] { new AulaProposta(1, 1, 1, 9, null), new AulaProposta(1, 1, 3, 9, null) }, slots, vazio, vazio, 3, 2);
        Assert.Contains(v5, v => v.Tipo == TipoViolacao.DisciplinaDia);
        // duas disciplinas no mesmo horário da turma
        var v6 = GeradorHorario.Verificar(new[] { new AulaProposta(1, 1, 1, 1, null), new AulaProposta(1, 1, 1, 2, null) }, slots, vazio, vazio, 3, 2);
        Assert.Contains(v6, v => v.Tipo == TipoViolacao.TurmaDuplicada);
        // ocupado em turma fixa
        var v7 = GeradorHorario.Verificar(new[] { new AulaProposta(1, 1, 1, 1, 7) }, slots, vazio, new HashSet<(int, int, int)> { (7, 1, 1) }, 3, 2);
        Assert.Contains(v7, v => v.Tipo == TipoViolacao.ProfessorDuplo);
    }

    [Fact] public void Grade_valida_nao_tem_violacao()
        => Assert.Empty(GeradorHorario.Verificar(new[] { new AulaProposta(1, 1, 1, 1, 7), new AulaProposta(1, 1, 2, 1, 7), new AulaProposta(1, 2, 1, 2, 8) },
            Manha, new HashSet<(int, int, int)>(), new HashSet<(int, int, int)>(), 3, 2));

    // ---------- cenários planejados: existe solução, então o gerador tem que achar ----------

    // Monta uma grade válida ao acaso (a "resposta"), e devolve os pedidos e indisponibilidades compatíveis com ela.
    static (EntradaGerador E, int Aulas) Planejado(int semente, int turmas, int disciplinas, int professores, int maxSeg, int maxDia)
    {
        var rnd = new Random(semente);
        var e = Nova(maxSeg, maxDia, semente);
        for (int t = 1; t <= turmas; t++) Turma(e, t);
        var prof = new int[turmas + 1, disciplinas + 1];
        for (int t = 1; t <= turmas; t++) for (int d = 1; d <= disciplinas; d++) prof[t, d] = rnd.Next(1, professores + 1);

        var aulas = new List<AulaProposta>();
        var vazio = new HashSet<(int, int, int)>();
        var cells = new List<(int t, int dia, int slot)>();
        for (int t = 1; t <= turmas; t++) for (int dia = 1; dia <= 5; dia++) foreach (var s in Manha) cells.Add((t, dia, s.Id));
        foreach (var (t, dia, slot) in cells.OrderBy(_ => rnd.Next()))
            for (int tent = 0; tent < 12; tent++)
            {
                int d = rnd.Next(1, disciplinas + 1);
                var nova = new AulaProposta(t, dia, slot, d, prof[t, d]);
                var teste = aulas.Append(nova).ToList();
                if (GeradorHorario.Verificar(teste, e.Slots, vazio, vazio, maxSeg, maxDia).Count == 0) { aulas = teste; break; }
            }
        foreach (var g in aulas.GroupBy(a => (a.TurmaId, a.DisciplinaId)))
            Pede(e, g.Key.TurmaId, g.Key.DisciplinaId, g.First().ProfessorId, g.Count());
        // indisponibilidades: alguns horários em que o professor NÃO tem aula na resposta
        var ocupado = aulas.Where(a => a.ProfessorId != null).Select(a => (a.ProfessorId!.Value, a.Dia, a.SlotId)).ToHashSet();
        for (int p = 1; p <= professores; p++)
            for (int dia = 1; dia <= 5; dia++)
                foreach (var s in Manha)
                    if (!ocupado.Contains((p, dia, s.Id)) && rnd.Next(100) < 35) e.Indisponiveis.Add((p, dia, s.Id));
        return (e, aulas.Count);
    }

    [Theory]
    [InlineData(2, 8, 6, 3, 2)]
    [InlineData(3, 9, 8, 3, 2)]
    [InlineData(4, 10, 8, 3, 2)]
    [InlineData(4, 10, 12, 2, 2)]
    [InlineData(3, 8, 5, 4, 2)]
    public void Existe_solucao_planejada_e_o_gerador_acha(int turmas, int disciplinas, int professores, int maxSeg, int maxDia)
    {
        int falhas = 0; var detalhes = new List<string>();
        for (int semente = 1; semente <= 6; semente++)
        {
            var (e, total) = Planejado(semente, turmas, disciplinas, professores, maxSeg, maxDia);
            var r = GeradorHorario.Gerar(e);
            var v = Confere(e, r);
            Assert.Empty(v);   // NUNCA pode quebrar uma regra dura, ache ou não a solução completa
            if (!r.Completo) { falhas++; detalhes.Add($"semente {semente}: faltaram {r.NaoColocadas.Sum(n => n.Faltam)} de {total} ({r.Nos} nós)"); }
        }
        Assert.True(falhas == 0, string.Join("; ", detalhes));
    }

    [Fact] public void Nunca_quebra_regra_mesmo_quando_o_pedido_e_impossivel()
    {
        for (int semente = 1; semente <= 8; semente++)
        {
            var rnd = new Random(semente);
            var e = Nova(rnd.Next(1, 4), rnd.Next(1, 3), semente);
            int turmas = rnd.Next(1, 4);
            for (int t = 1; t <= turmas; t++) Turma(e, t);
            for (int t = 1; t <= turmas; t++)
                for (int d = 1; d <= 7; d++) Pede(e, t, d, rnd.Next(1, 4), rnd.Next(0, 9));   // poucos professores, muita carga
            for (int p = 1; p <= 3; p++) for (int dia = 1; dia <= 5; dia++) foreach (var s in Manha) if (rnd.Next(100) < 30) e.Indisponiveis.Add((p, dia, s.Id));
            var r = GeradorHorario.Gerar(e);
            Assert.Empty(Confere(e, r));
            // contabilidade: colocadas + faltantes = pedidas
            foreach (var g in e.Pedidos.Where(p => p.Carga > 0))
            {
                int col = r.Aulas.Count(a => a.TurmaId == g.TurmaId && a.DisciplinaId == g.DisciplinaId);
                int falt = r.NaoColocadas.Where(n => n.TurmaId == g.TurmaId && n.DisciplinaId == g.DisciplinaId).Sum(n => n.Faltam);
                Assert.Equal(g.Carga, col + falt);
            }
        }
    }
}
