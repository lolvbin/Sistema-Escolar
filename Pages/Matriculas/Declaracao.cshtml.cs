using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Matriculas;

public class DeclaracaoModel : PageModel
{
    private readonly EscolaContext _db;
    public DeclaracaoModel(EscolaContext db) => _db = db;

    public Matricula Matricula { get; set; } = null!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var m = await _db.Matriculas.AsNoTracking().Include(x => x.Aluno).Include(x => x.Turma).FirstOrDefaultAsync(x => x.Id == id);
        if (m == null) return NotFound();
        Matricula = m;
        return Page();
    }
}
