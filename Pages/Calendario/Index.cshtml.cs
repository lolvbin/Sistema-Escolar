using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Calendario;

public class IndexModel : PageModel
{
    private readonly EscolaContext _db;
    public IndexModel(EscolaContext db) => _db = db;

    [Microsoft.AspNetCore.Mvc.BindProperty(SupportsGet = true)] public int? Ano { get; set; }
    [Microsoft.AspNetCore.Mvc.BindProperty(SupportsGet = true)] public int? Mes { get; set; }

    public DateTime Primeiro { get; set; }
    public List<Evento> Eventos { get; set; } = new();

    public async Task OnGetAsync()
    {
        var hoje = DateTime.Today;
        var ano = Ano is >= 2000 and <= 2100 ? Ano.Value : hoje.Year;
        var mes = Mes is >= 1 and <= 12 ? Mes.Value : hoje.Month;
        Primeiro = new DateTime(ano, mes, 1);
        var ultimo = Primeiro.AddMonths(1).AddDays(-1);

        // Eventos que tocam o mês (começam antes e terminam dentro, etc.)
        Eventos = await _db.Eventos.AsNoTracking()
            .Where(e => e.Inicio <= ultimo && (e.Fim ?? e.Inicio) >= Primeiro)
            .OrderBy(e => e.Inicio).ThenBy(e => e.Titulo).ToListAsync();
    }
}
