using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Horarios;

public class IndexModel : PageModel
{
    private readonly EscolaContext _db;
    public IndexModel(EscolaContext db) => _db = db;

    [Microsoft.AspNetCore.Mvc.BindProperty(SupportsGet = true)] public int? Ano { get; set; }
    public record Linha(Data.Turma Turma, int Aulas, int Previstas, int Slots);
    public List<Linha> Turmas { get; set; } = new();
    public List<Professor> Professores { get; set; } = new();
    public int TotalHorarios { get; set; }

    public async Task OnGetAsync()
    {
        Ano ??= DateTime.Today.Year;
        TotalHorarios = await _db.HorariosAula.CountAsync();
        var turmas = await _db.Turmas.AsNoTracking().Where(t => t.Ativa && t.AnoLetivo == Ano)
            .OrderBy(t => t.Serie).ThenBy(t => t.Letra).ToListAsync();
        var ids = turmas.Select(t => t.Id).ToList();
        var aulas = await _db.Grade.AsNoTracking().Where(g => ids.Contains(g.TurmaId)).GroupBy(g => g.TurmaId)
            .Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        var previstas = await _db.Set<TurmaDisciplina>().AsNoTracking().Where(x => ids.Contains(x.TurmaId)).GroupBy(x => x.TurmaId)
            .Select(g => new { g.Key, N = g.Sum(x => x.AulasSemanais) }).ToDictionaryAsync(x => x.Key, x => x.N);
        var slots = await _db.HorariosAula.AsNoTracking().Where(h => !h.Intervalo).GroupBy(h => h.Turno)
            .Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        Turmas = turmas.Select(t => new Linha(t, aulas.GetValueOrDefault(t.Id), previstas.GetValueOrDefault(t.Id), slots.GetValueOrDefault(t.Turno!.Value) * 5)).ToList();
        Professores = await _db.Professores.AsNoTracking().Where(p => p.Ativo).OrderBy(p => p.Nome).ToListAsync();
    }
}
