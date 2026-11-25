using SistemaEscolar.Data;

namespace SistemaEscolar.Tests;

public class ComunicadoTests
{
    static readonly DateTime Hoje = new(2026, 11, 25);
    static Comunicado C(PublicoComunicado p, int? turma = null, int diasValidade = 5) =>
        new() { Publico = p, TurmaId = turma, ValidoAte = Hoje.AddDays(diasValidade) };
    static readonly int[] Nenhuma = Array.Empty<int>();

    [Fact] public void Vigente_ate_o_dia_da_validade_inclusive()
    {
        Assert.True(C(PublicoComunicado.Todos, diasValidade: 0).Vigente(Hoje));
        Assert.False(C(PublicoComunicado.Todos, diasValidade: -1).Vigente(Hoje));
    }

    [Fact] public void Vencido_nao_aparece_para_ninguem()
    {
        foreach (var perfil in Enum.GetValues<Perfil>())
            Assert.False(C(PublicoComunicado.Todos, diasValidade: -1).VisivelPara(perfil, Nenhuma, Hoje));
    }

    [Fact] public void Todos_ve_qualquer_perfil()
    {
        foreach (var perfil in Enum.GetValues<Perfil>())
            Assert.True(C(PublicoComunicado.Todos).VisivelPara(perfil, Nenhuma, Hoje));
    }

    [Theory]
    [InlineData(PublicoComunicado.Responsaveis, Perfil.Responsavel)]
    [InlineData(PublicoComunicado.Alunos, Perfil.Aluno)]
    [InlineData(PublicoComunicado.Professores, Perfil.Professor)]
    [InlineData(PublicoComunicado.Funcionarios, Perfil.Funcionario)]
    public void Publico_especifico_so_para_o_seu_perfil(PublicoComunicado publico, Perfil dono)
    {
        foreach (var perfil in Enum.GetValues<Perfil>())
            Assert.Equal(perfil == dono, C(publico).VisivelPara(perfil, Nenhuma, Hoje));
    }

    [Fact] public void Comunicado_de_turma_so_para_quem_e_da_turma()
    {
        var c = C(PublicoComunicado.Todos, turma: 7);
        Assert.True(c.VisivelPara(Perfil.Responsavel, new[] { 7 }, Hoje));
        Assert.True(c.VisivelPara(Perfil.Professor, new[] { 3, 7 }, Hoje));
        Assert.False(c.VisivelPara(Perfil.Aluno, new[] { 3 }, Hoje));
        Assert.False(c.VisivelPara(Perfil.Responsavel, Nenhuma, Hoje));
    }

    [Fact] public void Responsavel_de_turma_nao_ve_o_que_e_so_de_alunos()
        => Assert.False(C(PublicoComunicado.Alunos, turma: 7).VisivelPara(Perfil.Responsavel, new[] { 7 }, Hoje));
}
