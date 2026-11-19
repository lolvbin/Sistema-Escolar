using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Professores;

public class EditModel : PageModel
{
    private readonly EscolaContext _db;
    public EditModel(EscolaContext db) => _db = db;

    [BindProperty] public Professor Professor { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var p = await _db.Professores.FindAsync(id);
        if (p == null) return NotFound();
        Professor = p;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        Professor.Validar(ModelState);
        if (!ModelState.IsValid) return Page();

        var p = await _db.Professores.Include(x => x.Usuario).FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();

        Professor.Normalizar();
        if (await _db.Professores.AnyAsync(x => x.Cpf == Professor.Cpf && x.Id != id))
            ModelState.AddModelError("Cpf", "Já existe um professor com este CPF");
        if (await _db.Usuarios.AnyAsync(u => u.Email == Professor.Email && u.Id != p.UsuarioId))
            ModelState.AddModelError("Email", "Este e-mail já tem um login no sistema");
        if (!ModelState.IsValid) return Page();

        p.Nome = Professor.Nome;
        p.DataNascimento = Professor.DataNascimento;
        p.Cpf = Professor.Cpf;
        p.Telefone = Professor.Telefone;
        p.Email = Professor.Email;
        p.Formacao = Professor.Formacao;

        // O login acompanha nome e e-mail
        if (p.Usuario != null)
        {
            p.Usuario.Nome = p.Nome;
            p.Usuario.Email = p.Email;
        }

        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
