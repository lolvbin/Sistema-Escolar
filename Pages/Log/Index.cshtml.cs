using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Log;

public class IndexModel : PageModel
{
    private readonly EscolaContext _db;
    public IndexModel(EscolaContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public string? Acao { get; set; }
    [BindProperty(SupportsGet = true)] public string? Busca { get; set; }
    [BindProperty(SupportsGet = true)] public string? De { get; set; }
    [BindProperty(SupportsGet = true)] public string? Ate { get; set; }
    public List<RegistroLog> Itens { get; set; } = new();
    public bool Cortou { get; set; }
    const int Limite = 200;

    public async Task OnGetAsync()
    {
        IQueryable<RegistroLog> q = _db.Registros.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(Acao)) q = q.Where(r => r.Acao == Acao);
        if (!string.IsNullOrWhiteSpace(Busca))
        {
            var b = $"%{Busca.Trim()}%";
            q = q.Where(r => EF.Functions.ILike(r.Detalhe, b) || EF.Functions.ILike(r.UsuarioNome, b));
        }
        if (RegrasFinanceiro.LerData(De, out var de)) q = q.Where(r => r.Em >= de.Date);
        if (RegrasFinanceiro.LerData(Ate, out var ate)) q = q.Where(r => r.Em < ate.Date.AddDays(1));
        Itens = await q.OrderByDescending(r => r.Em).ThenByDescending(r => r.Id).Take(Limite + 1).ToListAsync();
        if (Itens.Count > Limite) { Cortou = true; Itens.RemoveAt(Limite); }
    }
}
