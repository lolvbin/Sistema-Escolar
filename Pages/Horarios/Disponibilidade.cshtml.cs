using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Horarios;

// Horários em que o professor NÃO pode dar aula. A grade das turmas respeita isso.
public class DisponibilidadeModel : PageModel
{
    private readonly EscolaContext _db;
    public DisponibilidadeModel(EscolaContext db) => _db = db;

    public Data.Professor Professor { get; set; } = null!;
    public List<(Turno Turno, List<HorarioAula> Horarios)> Turnos { get; set; } = new();
    public HashSet<(int Dia, int SlotId)> Marcados { get; set; } = new();   // indisponível
    public Dictionary<(int Dia, int SlotId), string> ComAula { get; set; } = new();
    public List<string> Erros { get; set; } = new();
    [TempData] public string? Aviso { get; set; }

    private async Task<bool> Carregar(int id)
    {
        var p = await _db.Professores.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return false;
        Professor = p;
        var horarios = await _db.HorariosAula.AsNoTracking().Where(h => !h.Intervalo).OrderBy(h => h.Turno).ThenBy(h => h.Inicio).ToListAsync();
        Turnos = horarios.GroupBy(h => h.Turno).Select(g => (g.Key, g.ToList())).ToList();
        // aulas que o professor já tem (não dá para marcar indisponível sem ajustar a grade)
        var aulas = await (from g in _db.Grade.AsNoTracking()
                           join td in _db.Set<TurmaDisciplina>().AsNoTracking() on new { g.TurmaId, g.DisciplinaId } equals new { td.TurmaId, td.DisciplinaId }
                           where td.ProfessorId == id && g.Turma!.Ativa && g.Turma.AnoLetivo >= DateTime.Today.Year
                           select new { g.DiaSemana, g.HorarioAulaId, g.Turma!.Serie, g.Turma.Letra }).ToListAsync();
        foreach (var a in aulas) ComAula[(a.DiaSemana, a.HorarioAulaId)] = $"{a.Serie?.Nome()} {a.Letra}";
        return true;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await Carregar(id)) return NotFound();
        Marcados = (await _db.Indisponibilidades.AsNoTracking().Where(x => x.ProfessorId == id).ToListAsync())
            .Select(x => (x.DiaSemana, x.HorarioAulaId)).ToHashSet();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!await Carregar(id)) return NotFound();
        var validos = Turnos.SelectMany(t => t.Horarios).Select(h => h.Id).ToHashSet();
        foreach (var h in validos)
            for (int d = 1; d <= 5; d++)
                if (Request.Form.ContainsKey($"ind_{d}_{h}")) Marcados.Add((d, h));

        foreach (var m in Marcados.Where(m => ComAula.ContainsKey(m)))
            Erros.Add($"{RegrasHorario.Dias[m.Dia - 1]}: {Turnos.SelectMany(t => t.Horarios).First(h => h.Id == m.SlotId).Rotulo} já tem aula na turma {ComAula[m]}. Mude a grade da turma antes.");
        if (Erros.Count > 0) return Page();

        var antes = await _db.Indisponibilidades.Where(x => x.ProfessorId == id).ToListAsync();
        _db.Indisponibilidades.RemoveRange(antes);
        _db.Indisponibilidades.AddRange(Marcados.Select(m => new ProfessorIndisponivel { ProfessorId = id, DiaSemana = m.Dia, HorarioAulaId = m.SlotId }));
        if (antes.Count != Marcados.Count || antes.Any(a => !Marcados.Contains((a.DiaSemana, a.HorarioAulaId))))
            _db.Registrar(User, Auditoria.Horarios, $"Disponibilidade de {Professor.Nome}: {antes.Count} → {Marcados.Count} horários indisponíveis");
        await _db.SaveChangesAsync();
        Aviso = "Disponibilidade salva.";
        return RedirectToPage(new { id });
    }
}
