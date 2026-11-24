using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages;

// Banco novo, sem nenhum administrador: esta tela cria o primeiro. Depois que existe um, ela some (404).
[AllowAnonymous]
public class PrimeiroAcessoModel : PageModel
{
    private readonly EscolaContext _db;
    public PrimeiroAcessoModel(EscolaContext db) => _db = db;

    [BindProperty] public string? Nome { get; set; }
    [BindProperty] public string? Email { get; set; }
    [BindProperty] public string? Senha { get; set; }
    [BindProperty] public string? Confirmar { get; set; }
    public Dictionary<string, string> Erros { get; set; } = new();

    public static async Task<bool> SemAdministrador(EscolaContext db) =>
        !await db.Usuarios.AnyAsync(u => u.Perfil == Perfil.Funcionario && u.Ativo && (u.Areas & Area.Usuarios) != 0);

    public async Task<IActionResult> OnGetAsync() => await SemAdministrador(_db) ? Page() : NotFound();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await SemAdministrador(_db)) return NotFound();

        var nome = (Nome ?? "").Trim();
        var email = (Email ?? "").Trim().ToLowerInvariant();
        if (nome == "") Erros["Nome"] = "Informe o nome";
        else if (nome.Length > 120 || !new NomeAttribute().IsValid(nome)) Erros["Nome"] = "Use apenas letras";
        if (email == "") Erros["Email"] = "Informe o e-mail";
        else if (email.Length > 120 || !new EmailAddressAttribute().IsValid(email)) Erros["Email"] = "E-mail inválido";
        else if (await _db.Usuarios.AnyAsync(u => u.Email == email)) Erros["Email"] = "Este e-mail já tem um login no sistema";
        var erroSenha = Autenticacao.ValidarSenhaNova(Senha);
        if (erroSenha != null) Erros["Senha"] = erroSenha;
        if (Senha != Confirmar) Erros["Confirmar"] = "A confirmação não confere";
        if (Erros.Count > 0) return Page();

        var u = new Usuario
        {
            Nome = nome, Email = email, Perfil = Perfil.Funcionario, Cargo = Cargo.Administrador, Areas = AreaExt.Todas,
            SenhaHash = Data.Senha.Gerar(Senha!), Ativo = true
        };
        _db.Usuarios.Add(u);
        await _db.SaveChangesAsync();
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, Autenticacao.CriarPrincipal(u));
        return LocalRedirect("/");
    }
}
