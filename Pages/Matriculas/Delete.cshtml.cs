using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Matriculas;

public class DeleteModel : PageModel
{
    private readonly EscolaContext _db;
    public DeleteModel(EscolaContext db) => _db = db;

    public Matricula Matricula { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var m = await _db.Matriculas.FindAsync(id);
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
        }
        return RedirectToPage("Index");
    }
}
