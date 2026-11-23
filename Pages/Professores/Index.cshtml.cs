using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Professores;

public class IndexModel : PageModel
{
    private readonly EscolaContext _db;
    public IndexModel(EscolaContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public string? Busca { get; set; }
    [BindProperty(SupportsGet = true)] public string Situacao { get; set; } = "";

    public List<Professor> Itens { get; set; } = new();

    public async Task OnGetAsync()
    {
        var q = _db.Professores.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(Busca))
            q = q.Where(p => EF.Functions.ILike(p.Nome, $"%{Busca}%")
                          || EF.Functions.ILike(p.Email, $"%{Busca}%")
                          || p.Cpf.Contains(Busca));
        if (Situacao == "ativos") q = q.Where(p => p.Ativo);
        if (Situacao == "inativos") q = q.Where(p => !p.Ativo);

        Itens = await q.OrderBy(p => p.Nome).Take(200).ToListAsync();
    }

    // Inativar bloqueia também o login do professor.
    public async Task<IActionResult> OnPostAlternarAsync(int id)
    {
        var p = await _db.Professores.Include(x => x.Usuario).FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();

        p.Ativo = !p.Ativo;
        if (p.Usuario != null) p.Usuario.Ativo = p.Ativo;
        await _db.SaveChangesAsync();

        TempData["Aviso"] = p.Ativo ? $"{p.Nome} reativado." : $"{p.Nome} inativado (o login foi bloqueado).";
        return RedirectToPage(new { Busca, Situacao });
    }

    // O funcionário gera uma senha provisória; o professor é obrigado a trocá-la no primeiro acesso.
    public async Task<IActionResult> OnPostNovaSenhaAsync(int id)
    {
        var p = await _db.Professores.Include(x => x.Usuario).FirstOrDefaultAsync(x => x.Id == id);
        if (p?.Usuario == null) return NotFound();

        var senha = Senha.Provisoria();
        p.Usuario.SenhaHash = Senha.Gerar(senha);
        p.Usuario.DeveTrocarSenha = true;
        await _db.SaveChangesAsync();

        TempData["Aviso"] = $"Nova senha provisória de {p.Nome}: {senha} (mostrada só agora)";
        return RedirectToPage(new { Busca, Situacao });
    }
}
