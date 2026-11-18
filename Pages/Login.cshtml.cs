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
        var u = await _db.Usuarios.AsNoTracking().FirstOrDefaultAsync(x => x.Email == email);

        // Sempre calcula um hash, exista o usuário ou não
        var ok = Data.Senha.Conferir(Senha ?? "", u?.SenhaHash ?? Data.Senha.Falso);
        if (u == null || !u.Ativo || !ok)
        {
            Erro = "E-mail ou senha inválidos.";
            return Page();
        }

        var id = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, u.Id.ToString()),
            new Claim(ClaimTypes.Name, u.Nome),
            new Claim(ClaimTypes.Email, u.Email),
            new Claim(ClaimTypes.Role, u.Perfil.ToString()),
        }, CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(id));

        return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/");
    }
}
