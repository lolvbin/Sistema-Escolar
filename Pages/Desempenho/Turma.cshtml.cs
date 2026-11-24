using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Desempenho;

public class TurmaModel : PageModel
{
    private readonly EscolaContext _db;
    public TurmaModel(EscolaContext db) => _db = db;

    public Turma Turma { get; set; } = null!;
    public List<string> Disciplinas { get; set; } = new();
    public record Linha(int MatriculaId, string Aluno, ResultadoAluno Resultado);
    public List<Linha> Alunos { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var t = await _db.Turmas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (t == null) return NotFound();
        Turma = t;

        var discs = await _db.Set<TurmaDisciplina>().AsNoTracking().Where(x => x.TurmaId == id)
            .Select(x => new { x.DisciplinaId, x.Disciplina!.Nome }).OrderBy(x => x.Nome).ToListAsync();
        var d = discs.Select(x => (x.DisciplinaId, x.Nome)).ToList();
        Disciplinas = discs.Select(x => x.Nome).ToList();

        var mats = await _db.Matriculas.AsNoTracking().Where(m => m.TurmaId == id)
            .Select(m => new { m.Id, Aluno = m.Aluno!.Nome }).OrderBy(m => m.Aluno).ToListAsync();
        var ids = mats.Select(m => m.Id).ToList();
        var notas = await _db.Notas.AsNoTracking().Where(n => ids.Contains(n.MatriculaId)).ToListAsync();
        var faltas = await _db.Frequencias.AsNoTracking().Where(f => ids.Contains(f.MatriculaId)).ToListAsync();
        var aulas = await _db.Aulas.AsNoTracking().Where(a => a.TurmaId == id).ToListAsync();

        foreach (var m in mats)
            Alunos.Add(new Linha(m.Id, m.Aluno, ResultadoAluno.Calcular(d,
                notas.Where(n => n.MatriculaId == m.Id).ToList(),
                faltas.Where(f => f.MatriculaId == m.Id).ToList(), aulas)));
        return Page();
    }
}
