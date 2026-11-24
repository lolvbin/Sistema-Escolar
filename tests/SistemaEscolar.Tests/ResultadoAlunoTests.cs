using SistemaEscolar.Data;

namespace SistemaEscolar.Tests;

public class ResultadoAlunoTests
{
    static Nota N(int disc, int bim, decimal v) => new() { MatriculaId = 1, DisciplinaId = disc, Bimestre = bim, Valor = v };

    [Fact]
    public void Calcula_media_frequencia_e_situacao_geral()
    {
        var discs = new[] { (1, "Português"), (2, "Matemática") };
        var notas = new List<Nota> { N(1, 1, 8), N(1, 2, 8), N(1, 3, 8), N(1, 4, 8), N(2, 1, 5) };
        var faltas = new List<Frequencia> { new() { MatriculaId = 1, DisciplinaId = 1, Bimestre = 1, Faltas = 5 } };
        var aulas = new List<AulasDadas> { new() { TurmaId = 1, DisciplinaId = 1, Bimestre = 1, Aulas = 20 } };

        var r = ResultadoAluno.Calcular(discs, notas, faltas, aulas);

        Assert.Equal(2, r.Linhas.Count);
        Assert.Equal(8.0m, r.Linhas[0].Media);
        Assert.Equal(Situacao.Aprovado, r.Linhas[0].Situacao);
        Assert.Equal(75.0m, r.Linhas[0].Freq);
        Assert.Equal(5.0m, r.Linhas[1].Media);                  // média parcial das notas já lançadas
        Assert.Equal(Situacao.EmAndamento, r.Linhas[1].Situacao); // faltam bimestres
        Assert.Equal(Situacao.EmAndamento, r.Geral);          // uma disciplina ainda em andamento
        Assert.Equal(75.0m, r.FreqGeral);
    }

    [Fact]
    public void Sem_disciplinas_nao_quebra()
    {
        var r = ResultadoAluno.Calcular(Array.Empty<(int, string)>(), new List<Nota>(), new List<Frequencia>(), new List<AulasDadas>());
        Assert.Empty(r.Linhas);
        Assert.Null(r.FreqGeral);
    }
}
