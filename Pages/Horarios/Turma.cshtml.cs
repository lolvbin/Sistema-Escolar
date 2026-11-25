using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Horarios;

// Grade manual da turma: para cada dia e horário, escolhe-se a disciplina. O professor vem da atribuição da turma.
public class TurmaModel : PageModel
{
    private readonly EscolaContext _db;
    public TurmaModel(EscolaContext db) => _db = db;

    public Data.Turma Turma { get; set; } = null!;
    public List<HorarioAula> Horarios { get; set; } = new();
    public record Disc(int Id, string Nome, int? ProfessorId, string? Professor);
    public List<Disc> Disciplinas { get; set; } = new();

    // Valores dos campos: Celula["dia-horarioId"] = disciplinaId; Carga[disciplinaId] = aulas por semana
    [BindProperty] public Dictionary<string, string?> Celula { get; set; } = new();
    [BindProperty] public Dictionary<int, string?> Carga { get; set; } = new();
    public Dictionary<string, string> Erros { get; set; } = new();
    public Dictionary<int, string> ErrosCarga { get; set; } = new();
    public List<string> AvisosConflito { get; set; } = new();
    [TempData] public string? Aviso { get; set; }

    static string Chave(int dia, int horarioId) => $"{dia}-{horarioId}";

    private async Task<bool> Carregar(int id)
    {
        var t = await _db.Turmas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (t == null) return false;
        Turma = t;
        Horarios = await _db.HorariosAula.AsNoTracking().Where(h => h.Turno == t.Turno).OrderBy(h => h.Inicio).ToListAsync();
        var tds = await _db.Set<TurmaDisciplina>().AsNoTracking().Include(x => x.Disciplina).Include(x => x.Professor)
            .Where(x => x.TurmaId == id).OrderBy(x => x.Disciplina!.Nome).ToListAsync();
        Disciplinas = tds.Select(x => new Disc(x.DisciplinaId, x.Disciplina!.Nome, x.ProfessorId, x.Professor?.Nome)).ToList();
        _cargas = tds.ToDictionary(x => x.DisciplinaId, x => x.AulasSemanais);
        return true;
    }
    Dictionary<int, int> _cargas = new();
    public int CargaSalva(int discId) => _cargas.GetValueOrDefault(discId);

    /// <summary>Aulas já colocadas por disciplina (do que está na tela).</summary>
    public int Alocadas(int discId) => Celula.Values.Count(v => v == discId.ToString());

    // Ocupação do professor em outras turmas: (dia, horário, turma, professor)
    private async Task<List<(int Dia, int SlotId, int TurmaId, int ProfessorId)>> OcupadasEmOutrasTurmas(int turmaId, IEnumerable<int> professores)
    {
        var profs = professores.Distinct().ToList();
        var q = await (from g in _db.Grade.AsNoTracking()
                       join td in _db.Set<TurmaDisciplina>().AsNoTracking() on new { g.TurmaId, g.DisciplinaId } equals new { td.TurmaId, td.DisciplinaId }
                       where g.TurmaId != turmaId && td.ProfessorId != null && profs.Contains(td.ProfessorId.Value)
                       select new { g.DiaSemana, g.HorarioAulaId, g.TurmaId, ProfessorId = td.ProfessorId!.Value }).ToListAsync();
        return q.Select(x => (x.DiaSemana, x.HorarioAulaId, x.TurmaId, x.ProfessorId)).ToList();
    }

    private async Task<HashSet<(int ProfessorId, int Dia, int SlotId)>> Indisponiveis(IEnumerable<int> professores)
    {
        var profs = professores.Distinct().ToList();
        var l = await _db.Indisponibilidades.AsNoTracking().Where(x => profs.Contains(x.ProfessorId)).ToListAsync();
        return l.Select(x => (x.ProfessorId, x.DiaSemana, x.HorarioAulaId)).ToHashSet();
    }

    private string NomeProfessor(int id) => Disciplinas.FirstOrDefault(d => d.ProfessorId == id)?.Professor ?? "Professor";

    public async Task<string> NomeTurma(int id) => (await _db.Turmas.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id)) is { } t ? $"{t.Nome} {t.AnoLetivo}" : $"turma {id}";

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await Carregar(id)) return NotFound();
        var salvas = await _db.Grade.AsNoTracking().Where(g => g.TurmaId == id).ToListAsync();
        foreach (var g in salvas) Celula[Chave(g.DiaSemana, g.HorarioAulaId)] = g.DisciplinaId.ToString();
        foreach (var d in Disciplinas) Carga[d.Id] = CargaSalva(d.Id) == 0 ? "" : CargaSalva(d.Id).ToString();

        // Conflitos que surgiram depois (por exemplo, o professor da disciplina mudou)
        var porDisc = Disciplinas.ToDictionary(d => d.Id, d => d.ProfessorId);
        var conf = RegrasHorario.Conflitos(salvas.Select(g => (g.DiaSemana, g.HorarioAulaId, g.DisciplinaId)), id,
            d => porDisc.GetValueOrDefault(d), await OcupadasEmOutrasTurmas(id, porDisc.Values.Where(p => p != null).Select(p => p!.Value)));
        foreach (var c in conf) Erros[Chave(c.Dia, c.SlotId)] = await MensagemConflito(c);
        var indis = RegrasHorario.EmHorarioIndisponivel(salvas.Select(g => (g.DiaSemana, g.HorarioAulaId, g.DisciplinaId)), d => porDisc.GetValueOrDefault(d),
            await Indisponiveis(porDisc.Values.Where(p => p != null).Select(p => p!.Value)));
        foreach (var (dia, slot, prof) in indis) Erros[Chave(dia, slot)] = $"{NomeProfessor(prof)} está indisponível neste horário";
        if (conf.Count + indis.Count > 0) AvisosConflito.Add($"{conf.Count + indis.Count} problema(s) nesta grade (professor em dois lugares ou indisponível). Ajuste as células marcadas.");
        return Page();
    }

    private async Task<string> MensagemConflito(ConflitoHorario c)
    {
        var prof = await _db.Professores.AsNoTracking().Where(p => p.Id == c.ProfessorId).Select(p => p.Nome).FirstOrDefaultAsync();
        return $"{prof} já tem aula com {await NomeTurma(c.OutraTurmaId)} neste horário";
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!await Carregar(id)) return NotFound();
        var validasDisc = Disciplinas.Select(d => d.Id).ToHashSet();
        var validosHorarios = Horarios.Where(h => !h.Intervalo).Select(h => h.Id).ToHashSet();

        // Células enviadas: só dias 1-5 e horários de aula (não intervalo) deste turno
        var propostas = new List<(int Dia, int SlotId, int DisciplinaId)>();
        foreach (var h in Horarios.Where(x => !x.Intervalo))
            for (int d = 1; d <= 5; d++)
            {
                var v = (Celula.GetValueOrDefault(Chave(d, h.Id)) ?? "").Trim();
                if (v == "") continue;
                if (!int.TryParse(v, out var disc) || !validasDisc.Contains(disc)) { Erros[Chave(d, h.Id)] = "Disciplina inválida para esta turma"; continue; }
                propostas.Add((d, h.Id, disc));
            }

        // Carga semanal (opcional)
        var cargas = new Dictionary<int, int>();
        foreach (var d in Disciplinas)
        {
            var txt = (Carga.GetValueOrDefault(d.Id) ?? "").Trim();
            if (txt == "") { cargas[d.Id] = 0; continue; }
            if (!int.TryParse(txt, out var n) || n < 0 || n > RegrasHorario.MaxAulasSemanais) { ErrosCarga[d.Id] = $"0 a {RegrasHorario.MaxAulasSemanais}"; continue; }
            cargas[d.Id] = n;
        }

        // Conflito de professor com outras turmas
        var porDisc = Disciplinas.ToDictionary(d => d.Id, d => d.ProfessorId);
        var conf = RegrasHorario.Conflitos(propostas, id, d => porDisc.GetValueOrDefault(d),
            await OcupadasEmOutrasTurmas(id, porDisc.Values.Where(p => p != null).Select(p => p!.Value)));
        foreach (var c in conf) Erros[Chave(c.Dia, c.SlotId)] = await MensagemConflito(c);
        foreach (var (dia, slot, prof) in RegrasHorario.EmHorarioIndisponivel(propostas, d => porDisc.GetValueOrDefault(d),
                     await Indisponiveis(porDisc.Values.Where(p => p != null).Select(p => p!.Value))))
            Erros[Chave(dia, slot)] = $"{NomeProfessor(prof)} está indisponível neste horário";

        if (Erros.Count > 0 || ErrosCarga.Count > 0) return Page();

        // Salva: troca a grade da turma pela nova
        var antigas = await _db.Grade.Where(g => g.TurmaId == id).ToListAsync();
        _db.Grade.RemoveRange(antigas);
        _db.Grade.AddRange(propostas.Select(p => new AulaGrade { TurmaId = id, DiaSemana = p.Dia, HorarioAulaId = p.SlotId, DisciplinaId = p.DisciplinaId }));
        foreach (var td in await _db.Set<TurmaDisciplina>().Where(x => x.TurmaId == id).ToListAsync())
            td.AulasSemanais = cargas.GetValueOrDefault(td.DisciplinaId);
        _db.Registrar(User, Auditoria.Horarios, $"Grade da turma {Turma.Nome} {Turma.AnoLetivo}: {antigas.Count} → {propostas.Count} aulas");
        await _db.SaveChangesAsync();
        Aviso = "Grade salva.";
        return RedirectToPage(new { id });
    }
}
