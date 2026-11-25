using SistemaEscolar.Data;

namespace SistemaEscolar.Tests;

public class DisponibilidadeTests
{
    static readonly Func<int, int?> Profs = d => d switch { 10 => 1, 11 => 2, _ => null };
    static IReadOnlySet<(int, int, int)> Ind(params (int Prof, int Dia, int Slot)[] l) => l.ToHashSet();

    [Fact] public void Aula_em_horario_indisponivel_e_apontada()
    {
        var r = RegrasHorario.EmHorarioIndisponivel(new[] { (1, 5, 10), (2, 5, 10) }, Profs, Ind((1, 2, 5)));
        Assert.Single(r);
        Assert.Equal((2, 5, 1), r[0]);
    }

    [Fact] public void Outro_professor_outro_dia_ou_horario_nao_e_problema()
    {
        var ind = Ind((1, 2, 5));
        Assert.Empty(RegrasHorario.EmHorarioIndisponivel(new[] { (2, 5, 11) }, Profs, ind));   // outro professor
        Assert.Empty(RegrasHorario.EmHorarioIndisponivel(new[] { (1, 5, 10) }, Profs, ind));   // outro dia
        Assert.Empty(RegrasHorario.EmHorarioIndisponivel(new[] { (2, 6, 10) }, Profs, ind));   // outro horário
    }

    [Fact] public void Disciplina_sem_professor_nao_gera_problema()
        => Assert.Empty(RegrasHorario.EmHorarioIndisponivel(new[] { (2, 5, 99) }, Profs, Ind((1, 2, 5))));

    [Fact] public void Sem_indisponibilidade_tudo_liberado()
        => Assert.Empty(RegrasHorario.EmHorarioIndisponivel(new[] { (1, 1, 10), (2, 2, 11) }, Profs, Ind()));
}
