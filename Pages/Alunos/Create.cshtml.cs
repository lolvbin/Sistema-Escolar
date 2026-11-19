using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Alunos;

public class CreateModel : PageModel
{
    private readonly EscolaContext _db;
    public CreateModel(EscolaContext db) => _db = db;

    [BindProperty] public Aluno Aluno { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        Aluno.Validar(ModelState);
        if (!ModelState.IsValid) return Page();

        Aluno.Normalizar();
        if (await _db.Alunos.AnyAsync(a => a.Cpf == Aluno.Cpf))
        {
            ModelState.AddModelError("Cpf", "Já existe um aluno com este CPF");
            return Page();
        }

        _db.Alunos.Add(Aluno);
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
