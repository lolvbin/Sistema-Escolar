using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Disciplinas;

public class EditModel : PageModel
{
    private readonly EscolaContext _db;
    public EditModel(EscolaContext db) => _db = db;

    [BindProperty] public Disciplina Disciplina { get; set; } = new();
    [BindProperty] public List<int> SeriesIds { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var d = await _db.Disciplinas.Include(x => x.Series).FirstOrDefaultAsync(x => x.Id == id);
        if (d == null) return NotFound();
        Disciplina = d;
        ViewData["Marcadas"] = d.Series.Select(s => (int)s.Serie).ToHashSet();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        ViewData["Marcadas"] = SeriesIds.ToHashSet();
        Disciplina.Nome = Disciplina.Nome?.Trim() ?? "";

        var validas = SeriesIds.Where(i => Enum.IsDefined(typeof(Serie), i)).Distinct().ToList();
        if (validas.Count == 0) ViewData["ErroSeries"] = "Marque pelo menos uma série";
        if (await _db.Disciplinas.AnyAsync(x => x.Nome == Disciplina.Nome && x.Id != id))
            ModelState.AddModelError("Nome", "Já existe uma disciplina com este nome");
        if (!ModelState.IsValid || validas.Count == 0) return Page();

        var d = await _db.Disciplinas.Include(x => x.Series).FirstOrDefaultAsync(x => x.Id == id);
        if (d == null) return NotFound();

        d.Nome = Disciplina.Nome;
        _db.RemoveRange(d.Series);
        d.Series = validas.Select(i => new DisciplinaSerie { DisciplinaId = id, Serie = (Serie)i }).ToList();

        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
