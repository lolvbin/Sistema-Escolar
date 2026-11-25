using SistemaEscolar.Data;

namespace SistemaEscolar.Tests;

public class HorarioTests
{
    static TimeOnly H(string s) { RegrasHorario.LerHora(s, out var h); return h; }

    [Theory]
    [InlineData("07:30", true)] [InlineData("7:30", true)] [InlineData("23:59", true)] [InlineData(" 08:00 ", true)]
    [InlineData("25:00", false)] [InlineData("7h30", false)] [InlineData("", false)] [InlineData(null, false)] [InlineData("08:60", false)]
    public void Le_hora(string? txt, bool ok) => Assert.Equal(ok, RegrasHorario.LerHora(txt, out _));

    [Fact] public void Fim_precisa_ser_depois_do_inicio()
    {
        Assert.Null(RegrasHorario.ValidarIntervalo(H("07:30"), H("08:20")));
        Assert.NotNull(RegrasHorario.ValidarIntervalo(H("08:20"), H("08:20")));
        Assert.NotNull(RegrasHorario.ValidarIntervalo(H("09:00"), H("08:20")));
    }

    [Fact] public void Detecta_horarios_sobrepostos()
    {
        var ok = new[] { (H("07:30"), H("08:20")), (H("08:20"), H("09:10")), (H("09:30"), H("10:20")) };
        Assert.Empty(RegrasHorario.Sobrepostos(ok));   // encostar não é sobrepor
        var ruim = new[] { (H("07:30"), H("08:20")), (H("08:10"), H("09:00")) };
        Assert.Equal(new[] { 1 }, RegrasHorario.Sobrepostos(ruim));
        var fora = new[] { (H("09:00"), H("10:00")), (H("07:00"), H("09:30")) };   // fora de ordem
        Assert.Equal(new[] { 0 }, RegrasHorario.Sobrepostos(fora));
    }

    static readonly Func<int, int?> Profs = d => d switch { 10 => 1, 11 => 1, 12 => 2, _ => null };

    [Fact] public void Professor_em_duas_turmas_no_mesmo_horario_e_conflito()
    {
        var propostas = new[] { (Dia: 1, SlotId: 5, DisciplinaId: 10) };
        var ocupadas = new[] { (Dia: 1, SlotId: 5, TurmaId: 99, ProfessorId: 1) };
        var c = RegrasHorario.Conflitos(propostas, turmaId: 7, Profs, ocupadas);
        Assert.Single(c);
        Assert.Equal(99, c[0].OutraTurmaId);
        Assert.Equal(1, c[0].ProfessorId);
    }

    [Fact] public void Outro_dia_outro_horario_ou_outro_professor_nao_e_conflito()
    {
        var ocupadas = new[] { (Dia: 1, SlotId: 5, TurmaId: 99, ProfessorId: 1) };
        Assert.Empty(RegrasHorario.Conflitos(new[] { (2, 5, 10) }, 7, Profs, ocupadas));   // outro dia
        Assert.Empty(RegrasHorario.Conflitos(new[] { (1, 6, 10) }, 7, Profs, ocupadas));   // outro horário
        Assert.Empty(RegrasHorario.Conflitos(new[] { (1, 5, 12) }, 7, Profs, ocupadas));   // outro professor
    }

    [Fact] public void Disciplina_sem_professor_nao_gera_conflito()
        => Assert.Empty(RegrasHorario.Conflitos(new[] { (1, 5, 13) }, 7, Profs, new[] { (1, 5, 99, 1) }));

    [Fact] public void Mesmo_professor_duas_disciplinas_na_mesma_turma_no_mesmo_horario_so_conflita_com_outras_turmas()
    {
        // a própria turma não conta como "outra turma"
        var ocupadas = new[] { (Dia: 1, SlotId: 5, TurmaId: 7, ProfessorId: 1) };
        Assert.Empty(RegrasHorario.Conflitos(new[] { (1, 5, 10) }, 7, Profs, ocupadas));
    }
}
