using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Alunos;

public class HistoricoModel : PageModel
{
    private readonly EscolaContext _db;
    public HistoricoModel(EscolaContext db) => _db = db;

    public Aluno Aluno { get; set; } = null!;
    public record Ano(Matricula Matricula, ResultadoAluno? Resultado);
    public List<Ano> Anos { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var a = await _db.Alunos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (a == null) return NotFound();
        Aluno = a;
        var ms = await _db.Matriculas.AsNoTracking().Include(m => m.Turma)
            .Where(m => m.AlunoId == id).OrderBy(m => m.AnoLetivo).ToListAsync();
        foreach (var m in ms)
            Anos.Add(new Ano(m, m.TurmaId == null ? null : await ResultadoAlunoDb.CarregarAsync(_db, m.Id, m.TurmaId.Value)));
        return Page();
    }
}
