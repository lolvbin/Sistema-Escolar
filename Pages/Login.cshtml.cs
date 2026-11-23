using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly EscolaContext _db;
    public LoginModel(EscolaContext db) => _db = db;

    [BindProperty] public string? Email { get; set; }
    [BindProperty] public string? Senha { get; set; }
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    public string? Erro { get; set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true) return LocalRedirect("/");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var email = (Email ?? "").Trim().ToLowerInvariant();
        var kEmail = "login:" + email;
        var kIp = "ip:" + HttpContext.Connection.RemoteIpAddress;

        // Bloqueado por tentativas demais: nem confere a senha
        var espera = LimiteLogin.Bloqueado(kEmail, LimiteLogin.MaxPorEmail) ?? LimiteLogin.Bloqueado(kIp, LimiteLogin.MaxPorIp);
        if (espera != null)
        {
            Erro = $"Muitas tentativas. Tente novamente em {LimiteLogin.Minutos(espera.Value)}.";
            return Page();
        }

        var u = await _db.Usuarios.AsNoTracking().FirstOrDefaultAsync(x => x.Email == email);

        // Sempre calcula um hash, exista o usuário ou não
        var ok = Data.Senha.Conferir(Senha ?? "", u?.SenhaHash ?? Data.Senha.Falso);
        if (u == null || !u.Ativo || !ok)
        {
            LimiteLogin.Falhou(kEmail);
            LimiteLogin.Falhou(kIp);
            Erro = "E-mail ou senha inválidos.";
            return Page();
        }

        LimiteLogin.Limpar(kEmail);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, Autenticacao.CriarPrincipal(u));

        if (u.DeveTrocarSenha) return RedirectToPage("/Conta/Senha");
        return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/");
    }
}
