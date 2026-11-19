using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Professores;

public class CreateModel : PageModel
{
    private readonly EscolaContext _db;
    public CreateModel(EscolaContext db) => _db = db;

    [BindProperty] public Professor Professor { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        Professor.Validar(ModelState);
        if (!ModelState.IsValid) return Page();

        Professor.Normalizar();
        if (await _db.Professores.AnyAsync(p => p.Cpf == Professor.Cpf))
            ModelState.AddModelError("Cpf", "Já existe um professor com este CPF");
        if (await _db.Usuarios.AnyAsync(u => u.Email == Professor.Email))
            ModelState.AddModelError("Email", "Este e-mail já tem um login no sistema");
        if (!ModelState.IsValid) return Page();

        var senha = Senha.Provisoria();
        Professor.Ativo = true;
        Professor.Usuario = new Usuario
        {
            Nome = Professor.Nome,
            Email = Professor.Email,
            Perfil = Perfil.Professor,
            SenhaHash = Senha.Gerar(senha),
        };

        _db.Professores.Add(Professor);
        await _db.SaveChangesAsync(); // professor + login na mesma transação

        TempData["Aviso"] = $"Professor cadastrado. Login: {Professor.Email} · Senha provisória: {senha} (mostrada só agora)";
        return RedirectToPage("Index");
    }
}
