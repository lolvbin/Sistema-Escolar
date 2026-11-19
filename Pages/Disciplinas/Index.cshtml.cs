using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Disciplinas;

public class IndexModel : PageModel
{
    private readonly EscolaContext _db;
    public IndexModel(EscolaContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public string? Busca { get; set; }
    [BindProperty(SupportsGet = true)] public string Situacao { get; set; } = "";

    public List<Disciplina> Itens { get; set; } = new();
    public bool SemNenhuma { get; set; }

    public async Task OnGetAsync()
    {
        IQueryable<Disciplina> q = _db.Disciplinas.AsNoTracking().Include(d => d.Series);
        if (!string.IsNullOrWhiteSpace(Busca))
            q = q.Where(d => EF.Functions.ILike(d.Nome, $"%{Busca}%"));
        if (Situacao == "ativas") q = q.Where(d => d.Ativa);
        if (Situacao == "inativas") q = q.Where(d => !d.Ativa);

        Itens = await q.OrderBy(d => d.Nome).ToListAsync();
        SemNenhuma = !await _db.Disciplinas.AnyAsync();
    }

    public async Task<IActionResult> OnPostAlternarAsync(int id)
    {
        var d = await _db.Disciplinas.FindAsync(id);
        if (d == null) return NotFound();
        d.Ativa = !d.Ativa;
        await _db.SaveChangesAsync();
        return RedirectToPage(new { Busca, Situacao });
    }

    // Só adiciona o que ainda não existe (pelo nome), então pode ser clicado mais de uma vez.
    public async Task<IActionResult> OnPostCarregarPadraoAsync()
    {
        var existentes = await _db.Disciplinas.Select(d => d.Nome).ToListAsync();
        var novas = TabelasDisciplinas.Padrao().Where(p => !existentes.Contains(p.Nome)).ToList();
        foreach (var (nome, series) in novas)
            _db.Disciplinas.Add(new Disciplina { Nome = nome, Series = series.Select(s => new DisciplinaSerie { Serie = s }).ToList() });
        await _db.SaveChangesAsync();

        TempData["Aviso"] = $"{novas.Count} disciplinas adicionadas. Ajuste as séries de cada uma se precisar.";
        return RedirectToPage();
    }
}
