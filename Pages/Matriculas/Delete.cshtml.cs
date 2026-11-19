using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Matriculas;

public class DeleteModel : PageModel
{
    private readonly EscolaContext _db;
    public DeleteModel(EscolaContext db) => _db = db;

    public Matricula Matricula { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var m = await _db.Matriculas.Include(x => x.Aluno).FirstOrDefaultAsync(x => x.Id == id);
        if (m == null) return NotFound();
        Matricula = m;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var m = await _db.Matriculas.FindAsync(id);
        if (m != null)
        {
            _db.Matriculas.Remove(m);
            await _db.SaveChangesAsync();

            // Aluno só existe com matrícula: se não sobrou nenhuma, remove o aluno também.
            if (!await _db.Matriculas.AnyAsync(x => x.AlunoId == m.AlunoId))
            {
                var aluno = await _db.Alunos.FindAsync(m.AlunoId);
                if (aluno != null)
                {
                    _db.Alunos.Remove(aluno);
                    await _db.SaveChangesAsync();
                }
            }
        }
        return RedirectToPage("Index");
    }
}
