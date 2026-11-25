using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Horarios;

// Gerador automático: monta uma SUGESTÃO de grade para as turmas escolhidas. Nada é gravado até clicar em "Aplicar".
public class GeradorModel : PageModel
{
    private readonly EscolaContext _db;
    public GeradorModel(EscolaContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public int? Ano { get; set; }
    [BindProperty] public List<int> Selecionadas { get; set; } = new();
    [BindProperty] public Dictionary<int, string?> Plano { get; set; } = new();     // turmaId → "dia-horário-disciplina,..."
    [BindProperty] public string? Seguidas { get; set; }
    [BindProperty] public string? PorDia { get; set; }
    public string? ErroRegras { get; set; }
    [TempData] public string? Aviso { get; set; }
    public string? Erro { get; set; }

    public record Linha(Data.Turma Turma, int Horarios, int Carga, int NaGrade);
    public List<Linha> Linhas { get; set; } = new();

    public record Falta(string Disciplina, int Faltam, string Motivo);
    public class PreviaTurma
    {
        public Data.Turma Turma { get; set; } = null!;
        public GradeView Grade { get; set; } = new();
        public int Colocadas { get; set; }
        public int Pedidas { get; set; }
        public List<Falta> Faltas { get; set; } = new();
        public List<int> DiasComLacuna { get; set; } = new();
    }
    public List<PreviaTurma>? Previa { get; set; }
    public List<string> Problemas { get; set; } = new();
    public List<Data.Turma> SemCarga { get; set; } = new();
    public bool Completo { get; set; }
    public int TotalColocadas { get; set; }
    public int TotalPedidas { get; set; }

    static string Montar(IEnumerable<AulaProposta> aulas) => string.Join(",", aulas.OrderBy(a => a.Dia).ThenBy(a => a.SlotId).Select(a => $"{a.Dia}-{a.SlotId}-{a.DisciplinaId}"));

    static bool Ler(string? txt, int min, int max, out int v) => int.TryParse((txt ?? "").Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out v) && v >= min && v <= max;

    private async Task CarregarLinhas()
    {
        Ano ??= DateTime.Today.Year;
        if (!Matricula.AnosPermitidos.Contains(Ano.Value)) Ano = DateTime.Today.Year;
        var turmas = await _db.Turmas.AsNoTracking().Where(t => t.Ativa && t.AnoLetivo == Ano).OrderBy(t => t.Serie).ThenBy(t => t.Letra).ToListAsync();
        var ids = turmas.Select(t => t.Id).ToList();
        var cargas = await _db.Set<TurmaDisciplina>().AsNoTracking().Where(x => ids.Contains(x.TurmaId)).GroupBy(x => x.TurmaId)
            .Select(g => new { g.Key, N = g.Sum(x => x.AulasSemanais) }).ToDictionaryAsync(x => x.Key, x => x.N);
        var grade = await _db.Grade.AsNoTracking().Where(g => ids.Contains(g.TurmaId)).GroupBy(g => g.TurmaId)
            .Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        var slots = await _db.HorariosAula.AsNoTracking().Where(h => !h.Intervalo).GroupBy(h => h.Turno)
            .Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        Linhas = turmas.Select(t => new Linha(t, slots.GetValueOrDefault(t.Turno!.Value) * 5, cargas.GetValueOrDefault(t.Id), grade.GetValueOrDefault(t.Id))).ToList();
        Seguidas ??= RegrasHorario.MaxSeguidas.ToString();
        PorDia ??= RegrasHorario.MaxPorDia.ToString();
    }

    public async Task OnGetAsync()
    {
        await CarregarLinhas();
        Selecionadas = Linhas.Where(l => l.Carga > 0).Select(l => l.Turma.Id).ToList();
    }

    // Limites do gerador (guardados nas Configurações; quem tem a área Horários pode ajustar aqui)
    public async Task<IActionResult> OnPostRegrasAsync()
    {
        bool okSeg = Ler(Seguidas, 1, 8, out var seg);
        bool okDia = Ler(PorDia, 1, 4, out var dia);
        if (!okSeg) ErroRegras = "Aulas seguidas: use de 1 a 8";
        else if (!okDia) ErroRegras = "Aulas da disciplina por dia: use de 1 a 4";
        if (ErroRegras != null) { await CarregarLinhas(); Selecionadas = Linhas.Where(l => l.Carga > 0).Select(l => l.Turma.Id).ToList(); return Page(); }
        var c = await _db.Configuracoes.FirstAsync(x => x.Id == 1);
        if (c.MaxAulasSeguidas != seg || c.MaxAulasDisciplinaDia != dia)
            _db.Registrar(User, Auditoria.Horarios, $"Limites do gerador: seguidas {c.MaxAulasSeguidas} → {seg}; por dia {c.MaxAulasDisciplinaDia} → {dia}");
        c.MaxAulasSeguidas = seg; c.MaxAulasDisciplinaDia = dia;
        await _db.SaveChangesAsync();
        RegrasHorario.MaxSeguidas = seg; RegrasHorario.MaxPorDia = dia;
        Aviso = "Limites salvos.";
        return RedirectToPage(new { ano = Ano });
    }

    public async Task<IActionResult> OnPostGerarAsync()
    {
        await CarregarLinhas();
        if (Selecionadas.Count == 0) { Erro = "Marque pelo menos uma turma."; return Page(); }
        var ctx = await GeradorHorarioDb.CarregarAsync(_db, Ano!.Value, Selecionadas);
        SemCarga = ctx.SemCarga;
        if (ctx.Turmas.Count == 0) { Erro = "As turmas marcadas não têm \"aulas/semana\" definidas. Defina na grade de cada turma ou nas disciplinas."; return Page(); }

        ctx.Entrada.Semente = Random.Shared.Next(1, 1_000_000);
        var res = await Task.Run(() => GeradorHorario.Gerar(ctx.Entrada));

        string NomeTurma(int id) => ctx.Turmas.First(t => t.Id == id).Nome;
        string NomeProf(int? id) => id != null && ctx.Professores.TryGetValue(id.Value, out var n) ? n : "sem professor";
        foreach (var p in res.Problemas)
            Problemas.Add(p.Tipo switch
            {
                TipoProblema.TurmaExcede => $"{NomeTurma(p.TurmaId!.Value)}: {p.Necessarias} aulas por semana previstas, mas a turma só tem {p.Disponiveis} horários.",
                TipoProblema.ProfessorExcede => $"{NomeProf(p.ProfessorId)}: {p.Necessarias} aulas previstas, mas só tem {p.Disponiveis} horários livres (indisponibilidades e outras turmas).",
                _ => $"{NomeTurma(p.TurmaId!.Value)} · {ctx.Disciplinas.GetValueOrDefault(p.DisciplinaId!.Value)}: {p.Necessarias} aulas por semana, mas o limite de {ctx.Entrada.MaxPorDia} por dia permite só {p.Disponiveis}.",
            });

        Previa = new();
        Plano.Clear();
        foreach (var t in ctx.Turmas)
        {
            var doTurma = res.Aulas.Where(a => a.TurmaId == t.Id).ToList();
            var pv = new PreviaTurma { Turma = t, Colocadas = doTurma.Count, Pedidas = ctx.Entrada.Pedidos.Where(p => p.TurmaId == t.Id).Sum(p => p.Carga) };
            pv.Grade.Horarios = ctx.HorariosPorTurno.GetValueOrDefault(t.Turno!.Value) ?? new();
            foreach (var a in doTurma)
                pv.Grade.Celulas[(a.Dia, a.SlotId)] = new[] { ctx.Disciplinas.GetValueOrDefault(a.DisciplinaId) ?? "?", NomeProf(a.ProfessorId) };
            foreach (var n in res.NaoColocadas.Where(n => n.TurmaId == t.Id))
                pv.Faltas.Add(new Falta(ctx.Disciplinas.GetValueOrDefault(n.DisciplinaId) ?? "?", n.Faltam, n.Motivo));
            foreach (var l in res.Lacunas.Where(l => l.TurmaId == t.Id)) pv.DiasComLacuna.Add(l.Dia);
            Previa.Add(pv);
            Plano[t.Id] = Montar(doTurma);
        }
        TotalColocadas = res.Aulas.Count; TotalPedidas = Previa.Sum(p => p.Pedidas); Completo = res.Completo;
        ModelState.Remove(nameof(Plano));   // os valores novos valem, não os que vieram do formulário
        return Page();
    }

    public async Task<IActionResult> OnPostAplicarAsync()
    {
        await CarregarLinhas();
        var plano = new Dictionary<int, List<(int Dia, int SlotId, int DisciplinaId)>>();
        foreach (var (turmaId, txt) in Plano)
        {
            var l = new List<(int, int, int)>();
            foreach (var parte in (txt ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var p = parte.Split('-');
                if (p.Length != 3 || !int.TryParse(p[0], out var d) || !int.TryParse(p[1], out var s) || !int.TryParse(p[2], out var disc))
                { Erro = "A sugestão veio com dados inválidos. Gere de novo."; return Page(); }
                l.Add((d, s, disc));
            }
            plano[turmaId] = l;
        }
        if (plano.Count == 0) { Erro = "Não há sugestão para aplicar. Gere primeiro."; return Page(); }

        var erros = await GeradorHorarioDb.ValidarPlanoAsync(_db, Ano!.Value, plano);
        if (erros.Count > 0) { Erro = "Não foi possível aplicar: " + string.Join("; ", erros) + ". Gere de novo."; return Page(); }

        var ids = plano.Keys.ToList();
        var antigas = await _db.Grade.Where(g => ids.Contains(g.TurmaId)).ToListAsync();
        _db.Grade.RemoveRange(antigas);
        foreach (var (turmaId, aulas) in plano)
            _db.Grade.AddRange(aulas.Select(a => new AulaGrade { TurmaId = turmaId, DiaSemana = a.Dia, HorarioAulaId = a.SlotId, DisciplinaId = a.DisciplinaId }));
        int novas = plano.Values.Sum(l => l.Count);
        _db.Registrar(User, Auditoria.Horarios, $"Gerador automático ({Ano}): {novas} aulas em {plano.Count} turma(s), substituindo {antigas.Count} aulas da grade anterior");
        await _db.SaveChangesAsync();
        Aviso = $"Sugestão aplicada: {novas} aulas em {plano.Count} turma(s). Confira e ajuste na grade de cada turma, se precisar.";
        return RedirectToPage(new { ano = Ano });
    }
}
