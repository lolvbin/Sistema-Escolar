using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Disciplinas;

public class CreateModel : PageModel
{
    private readonly EscolaContext _db;
    public CreateModel(EscolaContext db) => _db = db;

    [BindProperty] public Disciplina Disciplina { get; set; } = new();
    [BindProperty] public List<int> SeriesIds { get; set; } = new();

    public void OnGet() => ViewData["Marcadas"] = new HashSet<int>();

    public async Task<IActionResult> OnPostAsync()
    {
        ViewData["Marcadas"] = SeriesIds.ToHashSet();
        Disciplina.Nome = Disciplina.Nome?.Trim() ?? "";

        var validas = SeriesIds.Where(i => Enum.IsDefined(typeof(Serie), i)).Distinct().ToList();
        if (validas.Count == 0) ViewData["ErroSeries"] = "Marque pelo menos uma série";
        if (await _db.Disciplinas.AnyAsync(d => d.Nome == Disciplina.Nome))
            ModelState.AddModelError("Nome", "Já existe uma disciplina com este nome");
        if (!ModelState.IsValid || validas.Count == 0) return Page();

        Disciplina.Ativa = true;
        Disciplina.Series = validas.Select(i => new DisciplinaSerie { Serie = (Serie)i }).ToList();
        _db.Disciplinas.Add(Disciplina);
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
