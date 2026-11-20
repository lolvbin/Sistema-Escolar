using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Calendario;

public class EventoModel : PageModel
{
    private readonly EscolaContext _db;
    public EventoModel(EscolaContext db) => _db = db;

    [BindProperty] public Evento Evento { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string? Data { get; set; } // dd/MM/yyyy sugerida ao criar

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id == null)
        {
            if (DateTime.TryParseExact(Data, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var d))
                Evento.Inicio = d;
            return Page();
        }
        var e = await _db.Eventos.FindAsync(id);
        if (e == null) return NotFound();
        Evento = e;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        Evento.Validar(ModelState);
        if (!ModelState.IsValid) return Page();
        Evento.Normalizar();

        if (id == null) _db.Eventos.Add(Evento);
        else
        {
            var e = await _db.Eventos.FindAsync(id);
            if (e == null) return NotFound();
            e.Titulo = Evento.Titulo; e.Tipo = Evento.Tipo; e.Inicio = Evento.Inicio;
            e.Fim = Evento.Fim; e.Descricao = Evento.Descricao;
        }
        await _db.SaveChangesAsync();
        return RedirectToPage("Index", new { ano = Evento.Inicio!.Value.Year, mes = Evento.Inicio.Value.Month });
    }

    public async Task<IActionResult> OnPostExcluirAsync(int id)
    {
        var e = await _db.Eventos.FindAsync(id);
        if (e == null) return NotFound();
        var (ano, mes) = (e.Inicio!.Value.Year, e.Inicio.Value.Month);
        _db.Eventos.Remove(e);
        await _db.SaveChangesAsync();
        return RedirectToPage("Index", new { ano, mes });
    }
}
