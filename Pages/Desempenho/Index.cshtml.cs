using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Desempenho;

public class IndexModel : PageModel
{
    private readonly EscolaContext _db;
    public IndexModel(EscolaContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public int? Ano { get; set; }
    [BindProperty(SupportsGet = true)] public Serie? Serie { get; set; }

    public record Linha(Turma Turma, int Alunos);
    public List<Linha> Itens { get; set; } = new();

    public async Task OnGetAsync()
    {
        var q = _db.Turmas.AsNoTracking().AsQueryable();
        if (Ano != null) q = q.Where(t => t.AnoLetivo == Ano);
        if (Serie != null) q = q.Where(t => t.Serie == Serie);

        var dados = await q.OrderByDescending(t => t.AnoLetivo).ThenBy(t => t.Serie).ThenBy(t => t.Letra)
            .Select(t => new { Turma = t, Alunos = _db.Matriculas.Count(m => m.TurmaId == t.Id) })
            .Take(300).ToListAsync();
        Itens = dados.Select(d => new Linha(d.Turma, d.Alunos)).ToList();
    }
}
