using Microsoft.EntityFrameworkCore;
namespace SistemaEscolar.Data;

// Cálculo do desempenho de um aluno (por matrícula) a partir de dados já carregados.
// Usado pelo Boletim (aluno/responsável) e pelas telas de Desempenho (funcionário).
public record LinhaDisciplina(string Disciplina, decimal?[] Notas, decimal? Rec, decimal? Media, decimal? Freq, Situacao Situacao);

public record ResultadoAluno(List<LinhaDisciplina> Linhas, decimal? FreqGeral, Situacao Geral)
{
    public static ResultadoAluno Calcular(
        IEnumerable<(int Id, string Nome)> disciplinas,
        IReadOnlyCollection<Nota> notas, IReadOnlyCollection<Frequencia> faltas, IReadOnlyCollection<AulasDadas> aulas)
    {
        var linhas = new List<LinhaDisciplina>();
        foreach (var d in disciplinas)
        {
            var v = new decimal?[TabelaNotas.Bimestres];
            foreach (var n in notas.Where(n => n.DisciplinaId == d.Id && n.Bimestre >= 1 && n.Bimestre <= v.Length))
                v[n.Bimestre - 1] = n.Valor;
            var rec = notas.FirstOrDefault(n => n.DisciplinaId == d.Id && n.Bimestre == TabelaNotas.BimRecuperacao)?.Valor;
            var nf = faltas.Where(f => f.DisciplinaId == d.Id).Sum(f => f.Faltas);
            var na = aulas.Where(a => a.DisciplinaId == d.Id).Sum(a => a.Aulas);
            linhas.Add(new LinhaDisciplina(d.Nome, v, rec, Aprovacao.MediaFinal(v, rec), Aprovacao.Frequencia(nf, na), Aprovacao.DaDisciplina(v, rec)));
        }
        var freq = Aprovacao.Frequencia(faltas.Sum(f => f.Faltas), aulas.Sum(a => a.Aulas));
        return new ResultadoAluno(linhas, freq, Aprovacao.Geral(linhas.Select(l => l.Situacao), freq));
    }
}

public static class ResultadoAlunoDb
{
    /// <summary>Notas e frequência de uma matrícula enturmada (usado no Desempenho e no Histórico).</summary>
    public static async Task<ResultadoAluno> CarregarAsync(EscolaContext db, int matriculaId, int turmaId)
    {
        var discs = await db.Set<TurmaDisciplina>().AsNoTracking().Where(x => x.TurmaId == turmaId)
            .Select(x => new { x.DisciplinaId, x.Disciplina!.Nome }).OrderBy(x => x.Nome).ToListAsync();
        var notas = await db.Notas.AsNoTracking().Where(n => n.MatriculaId == matriculaId).ToListAsync();
        var faltas = await db.Frequencias.AsNoTracking().Where(f => f.MatriculaId == matriculaId).ToListAsync();
        var aulas = await db.Aulas.AsNoTracking().Where(a => a.TurmaId == turmaId).ToListAsync();
        return ResultadoAluno.Calcular(discs.Select(x => (x.DisciplinaId, x.Nome)), notas, faltas, aulas);
    }
}
