using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Financeiro;

public class ReciboModel : PageModel
{
    private readonly EscolaContext _db;
    public ReciboModel(EscolaContext db) => _db = db;

    public Cobranca Cobranca { get; set; } = null!;
    public Aluno Aluno { get; set; } = null!;
    public Matricula Matricula { get; set; } = null!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var c = await _db.Cobrancas.AsNoTracking().Include(x => x.Matricula).ThenInclude(m => m!.Aluno).FirstOrDefaultAsync(x => x.Id == id);
        if (c == null || !c.Paga) return NotFound(); // recibo só de cobrança paga
        Cobranca = c; Matricula = c.Matricula!; Aluno = c.Matricula!.Aluno!;
        return Page();
    }
}
