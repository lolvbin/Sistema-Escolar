using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Usuarios;

public class CreateModel : PageModel
{
    private readonly EscolaContext _db;
    public CreateModel(EscolaContext db) => _db = db;

    [BindProperty] public UsuarioForm Input { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        Input.Validar(ModelState);
        if (ModelState.IsValid && await _db.Usuarios.AnyAsync(u => u.Email == Input.Email))
            ModelState.AddModelError("Input.Email", "Este e-mail já tem um login no sistema");
        if (!ModelState.IsValid) return Page();

        var senha = Senha.Provisoria();
        _db.Usuarios.Add(new Usuario
        {
            Nome = Input.Nome!, Email = Input.Email!, Perfil = Perfil.Funcionario, Cargo = Input.Cargo, Areas = Input.Areas,
            SenhaHash = Senha.Gerar(senha), DeveTrocarSenha = true, Ativo = true
        });
        _db.Registrar(User, Auditoria.Usuarios, $"Criou o funcionário {Input.Nome} ({Input.Email}), cargo {Input.Cargo}, áreas {Input.Areas.Resumo()}");
        await _db.SaveChangesAsync();

        TempData["Aviso"] = $"Funcionário criado. Login: {Input.Email} · Senha provisória: {senha} (mostrada só agora; será pedida a troca no primeiro acesso)";
        return RedirectToPage("Index");
    }
}
