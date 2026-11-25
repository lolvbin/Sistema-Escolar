using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Comunicados;

// Histórico para os funcionários: vigentes e vencidos
public class IndexModel : PageModel
{
    private readonly EscolaContext _db;
    public IndexModel(EscolaContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public string? Situacao { get; set; }   // vigentes (padrão) | vencidos | todos
    [BindProperty(SupportsGet = true)] public string? Busca { get; set; }
    public List<Comunicado> Itens { get; set; } = new();
    [TempData] public string? Aviso { get; set; }

    public async Task OnGetAsync()
    {
        var hoje = DateTime.Today;
        IQueryable<Comunicado> q = _db.Comunicados.AsNoTracking().Include(c => c.Turma);
        if (Situacao == "vencidos") q = q.Where(c => c.ValidoAte < hoje);
        else if (Situacao != "todos") q = q.Where(c => c.ValidoAte >= hoje);
        if (!string.IsNullOrWhiteSpace(Busca))
        {
            var b = $"%{Busca.Trim()}%";
            q = q.Where(c => EF.Functions.ILike(c.Titulo, b) || EF.Functions.ILike(c.Texto ?? "", b));
        }
        Itens = await q.OrderByDescending(c => c.Fixado && c.ValidoAte >= hoje).ThenByDescending(c => c.CriadoEm).Take(200).ToListAsync();
    }

    public async Task<IActionResult> OnPostExcluirAsync(int id)
    {
        var c = await _db.Comunicados.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        _db.Comunicados.Remove(c);
        _db.Registrar(User, Auditoria.Comunicados, $"Excluiu o comunicado \"{c.Titulo}\"");
        await _db.SaveChangesAsync();
        Aviso = "Comunicado excluído.";
        return RedirectToPage(new { Situacao, Busca });
    }
}
