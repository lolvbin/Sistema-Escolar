using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Matriculas;

public class IndexModel : PageModel
{
    private readonly EscolaContext _db;
    public IndexModel(EscolaContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public string? Busca { get; set; }
    [BindProperty(SupportsGet = true)] public StatusMatricula? Status { get; set; }

    public List<Matricula> Itens { get; set; } = new();

    public async Task OnGetAsync()
    {
        var q = _db.Matriculas.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(Busca))
            q = q.Where(m => EF.Functions.ILike(m.AlunoNome, $"%{Busca}%")
                          || EF.Functions.ILike(m.Academico.Nome, $"%{Busca}%"));
        if (Status != null)
            q = q.Where(m => m.Status == Status);

        Itens = await q.OrderByDescending(m => m.Id).Take(200).ToListAsync();
    }
}
