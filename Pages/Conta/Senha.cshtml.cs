using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Conta;

// Troca da própria senha (qualquer perfil logado). Obrigatória para quem entrou com senha provisória.
public class SenhaModel : PageModel
{
    private readonly EscolaContext _db;
    public SenhaModel(EscolaContext db) => _db = db;

    [BindProperty] public string? Atual { get; set; }
    [BindProperty] public string? Nova { get; set; }
    [BindProperty] public string? Confirmar { get; set; }
    public Dictionary<string, string> Erros { get; set; } = new();
    public bool Forcada => User.HasClaim(Autenticacao.ClaimTrocarSenha, "1");
    [TempData] public string? Aviso { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        var uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var chave = "senha:" + uid;
        var espera = LimiteLogin.Bloqueado(chave, LimiteLogin.MaxPorEmail);
        if (espera != null) { Erros["Atual"] = $"Muitas tentativas. Tente novamente em {LimiteLogin.Minutos(espera.Value)}."; return Page(); }

        var u = await _db.Usuarios.FirstOrDefaultAsync(x => x.Id == uid && x.Ativo);
        if (u == null) return Forbid();

        if (!Senha.Conferir(Atual ?? "", u.SenhaHash))
        {
            LimiteLogin.Falhou(chave);
            Erros["Atual"] = "Senha atual incorreta";
            return Page();
        }
        var erro = Autenticacao.ValidarSenhaNova(Nova);
        if (erro != null) Erros["Nova"] = erro;
        else if (Nova == Atual) Erros["Nova"] = "A nova senha deve ser diferente da atual";
        if (Nova != Confirmar) Erros["Confirmar"] = "A confirmação não confere";
        if (Erros.Count > 0) return Page();

        LimiteLogin.Limpar(chave);
        u.SenhaHash = Senha.Gerar(Nova!);
        u.DeveTrocarSenha = false;
        await _db.SaveChangesAsync();

        // Renova o cookie (sem a marca de senha provisória)
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, Autenticacao.CriarPrincipal(u));
        Aviso = "Senha alterada com sucesso.";
        return RedirectToPage("/Index");
    }
}
