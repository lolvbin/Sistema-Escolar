using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Usuarios;

public class EditModel : PageModel
{
    private readonly EscolaContext _db;
    public EditModel(EscolaContext db) => _db = db;

    [BindProperty] public UsuarioForm Input { get; set; } = new();
    public int Id { get; set; }
    public bool Eu { get; set; }

    private int MeuId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private async Task<Usuario?> Achar(int id)
    {
        Id = id; Eu = id == MeuId;
        return await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == id && u.Perfil == Perfil.Funcionario);
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var u = await Achar(id);
        if (u == null) return NotFound();
        Input = new UsuarioForm
        {
            Nome = u.Nome, Email = u.Email, Cargo = u.Cargo, Ativo = u.Ativo,
            Marcadas = AreaExt.Individuais.Where(a => u.Areas.HasFlag(a)).Select(a => (int)a).ToList()
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var u = await Achar(id);
        if (u == null) return NotFound();

        Input.Validar(ModelState);
        if (ModelState.IsValid && await _db.Usuarios.AnyAsync(x => x.Email == Input.Email && x.Id != id))
            ModelState.AddModelError("Input.Email", "Este e-mail já tem um login no sistema");

        // O sistema nunca fica sem um administrador ativo
        if (ModelState.IsValid)
        {
            var outros = await _db.Usuarios.AsNoTracking().Where(x => x.Perfil == Perfil.Funcionario && x.Id != id)
                .Select(x => new { x.Ativo, x.Areas }).ToListAsync();
            var lista = outros.Select(x => (x.Ativo, x.Areas)).Append((Input.Ativo, Input.Areas));
            if (!RegrasUsuarios.RestaAdministrador(lista))
                ModelState.AddModelError("Input.Marcadas", "O sistema precisa de pelo menos um funcionário ativo com a área Usuários");
        }
        if (!ModelState.IsValid) return Page();

        var mudouAcesso = u.Areas != Input.Areas || u.Ativo != Input.Ativo || u.Email != Input.Email;
        _db.Registrar(User, Auditoria.Usuarios,
            $"Editou {u.Nome} ({u.Email}): áreas {u.Areas.Resumo()} → {Input.Areas.Resumo()}; ativo {u.Ativo} → {Input.Ativo}" + (u.Email != Input.Email ? $"; e-mail → {Input.Email}" : ""));
        u.Nome = Input.Nome!; u.Email = Input.Email!; u.Cargo = Input.Cargo; u.Areas = Input.Areas; u.Ativo = Input.Ativo;
        await _db.SaveChangesAsync();

        if (Eu)
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, Autenticacao.CriarPrincipal(u)); // vale na hora para mim
        else if (mudouAcesso)
            Sessoes.Revogar(u.Id);                                                                                          // o outro entra de novo

        TempData["Aviso"] = $"{u.Nome} atualizado." + (!Eu && mudouAcesso ? " Se estiver logado, será desconectado." : "");
        return RedirectToPage("Index");
    }

    // Gera uma senha provisória (o funcionário troca no próximo acesso)
    public async Task<IActionResult> OnPostNovaSenhaAsync(int id)
    {
        var u = await Achar(id);
        if (u == null) return NotFound();
        if (Eu) { TempData["Aviso"] = "Para trocar a sua própria senha use \"Alterar senha\" na tela inicial."; return RedirectToPage("Index"); }

        var senha = Senha.Provisoria();
        u.SenhaHash = Senha.Gerar(senha);
        u.DeveTrocarSenha = true;
        _db.Registrar(User, Auditoria.Acesso, $"Gerou nova senha provisória para o funcionário {u.Nome} ({u.Email})");
        await _db.SaveChangesAsync();
        Sessoes.Revogar(u.Id);
        TempData["Aviso"] = $"Nova senha provisória de {u.Nome}: {senha} (mostrada só agora)";
        return RedirectToPage("Index");
    }
}
