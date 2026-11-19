using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Turmas;

public class CreateModel : PageModel
{
    private readonly EscolaContext _db;
    public CreateModel(EscolaContext db) => _db = db;

    [BindProperty] public Turma Turma { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!Matricula.AnosPermitidos.Contains(Turma.AnoLetivo))
            ModelState.AddModelError("Turma.AnoLetivo", $"Ano letivo deve ser {Matricula.AnosPermitidos[0]} ou {Matricula.AnosPermitidos[1]}");
        Turma.Letra = (Turma.Letra ?? "").Trim().ToUpperInvariant();
        ModelState.Remove("Turma.Letra"); // revalida abaixo já normalizada
        if (!System.Text.RegularExpressions.Regex.IsMatch(Turma.Letra, "^[A-Z]{1,2}$"))
            ModelState.AddModelError("Turma.Letra", "Use 1 ou 2 letras (A, B, C...)");
        if (!ModelState.IsValid) return Page();

        if (await _db.Turmas.AnyAsync(t => t.AnoLetivo == Turma.AnoLetivo && t.Serie == Turma.Serie && t.Letra == Turma.Letra))
        {
            ModelState.AddModelError("Turma.Letra", "Já existe esta turma neste ano");
            return Page();
        }

        Turma.Ativa = true;
        _db.Turmas.Add(Turma);
        await _db.SaveChangesAsync();
        return RedirectToPage("Details", new { id = Turma.Id });
    }
}
