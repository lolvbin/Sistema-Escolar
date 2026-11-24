using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Desempenho;

public class AlunoModel : PageModel
{
    private readonly EscolaContext _db;
    public AlunoModel(EscolaContext db) => _db = db;

    public Matricula Matricula { get; set; } = null!;
    public ResultadoAluno Resultado { get; set; } = null!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var m = await _db.Matriculas.AsNoTracking().Include(x => x.Aluno).Include(x => x.Turma).FirstOrDefaultAsync(x => x.Id == id);
        if (m == null || m.TurmaId == null) return NotFound();
        Matricula = m;

        var discs = await _db.Set<TurmaDisciplina>().AsNoTracking().Where(x => x.TurmaId == m.TurmaId)
            .Select(x => new { x.DisciplinaId, x.Disciplina!.Nome }).OrderBy(x => x.Nome).ToListAsync();
        var notas = await _db.Notas.AsNoTracking().Where(n => n.MatriculaId == id).ToListAsync();
        var faltas = await _db.Frequencias.AsNoTracking().Where(f => f.MatriculaId == id).ToListAsync();
        var aulas = await _db.Aulas.AsNoTracking().Where(a => a.TurmaId == m.TurmaId).ToListAsync();
        Resultado = ResultadoAluno.Calcular(discs.Select(x => (x.DisciplinaId, x.Nome)), notas, faltas, aulas);
        return Page();
    }
}
