using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Matriculas;

public class CreateModel : PageModel
{
    private readonly EscolaContext _db;
    public CreateModel(EscolaContext db) => _db = db;

    [BindProperty] public Matricula Matricula { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        Matricula.Validar(ModelState);
        if (!ModelState.IsValid) return Page();

        // Toda matrícula nasce pendente (liberação virá do financeiro/funcionário).
        Matricula.Status = StatusMatricula.Pendente;
        Matricula.DataMatricula = DateTime.Today;
        Matricula.Normalizar();

        _db.Matriculas.Add(Matricula);
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
