using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Alunos;

public class IndexModel : PageModel
{
    private readonly EscolaContext _db;
    public IndexModel(EscolaContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public string? Busca { get; set; }

    public List<Aluno> Itens { get; set; } = new();

    public async Task OnGetAsync()
    {
        var q = _db.Alunos.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(Busca))
            q = q.Where(a => EF.Functions.ILike(a.Nome, $"%{Busca}%")
                          || EF.Functions.ILike(a.Academico.Nome, $"%{Busca}%"));

        Itens = await q.OrderBy(a => a.Nome).Take(200).ToListAsync();
    }
}
