using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Horarios;

// Horários de aula de cada turno (7:30 às 8:20, intervalo, ...). Servem de linhas para a grade das turmas.
public class AulasModel : PageModel
{
    private readonly EscolaContext _db;
    public AulasModel(EscolaContext db) => _db = db;

    public const int Novos = 3;   // linhas em branco por turno
    public List<HorarioAula> Existentes { get; set; } = new();
    public Dictionary<int, bool> EmUso { get; set; } = new();
    public Dictionary<string, string> Erros { get; set; } = new();
    [TempData] public string? Aviso { get; set; }

    private async Task Carregar(bool rastrear = false)
    {
        IQueryable<HorarioAula> q = _db.HorariosAula;
        if (!rastrear) q = q.AsNoTracking();
        Existentes = await q.OrderBy(h => h.Turno).ThenBy(h => h.Inicio).ToListAsync();
        var usados = await _db.Grade.AsNoTracking().Select(g => g.HorarioAulaId).Distinct().ToListAsync();
        EmUso = usados.ToDictionary(x => x, _ => true);
    }

    public async Task OnGetAsync() => await Carregar();

    // Valor enviado no formulário (para reexibir em caso de erro)
    public string V(string campo) => Request.HasFormContentType ? (string?)Request.Form[campo] ?? "" : "";

    public async Task<IActionResult> OnPostAsync()
    {
        await Carregar(rastrear: true);
        var f = Request.Form;
        var alteracoes = new List<string>();
        var remover = new List<HorarioAula>();
        var novos = new List<HorarioAula>();

        foreach (var turno in Enum.GetValues<Turno>())
        {
            var linhas = new List<(string Chave, HorarioAula H)>();
            foreach (var h in Existentes.Where(x => x.Turno == turno))
            {
                if (f.ContainsKey($"del_{h.Id}"))
                {
                    if (EmUso.ContainsKey(h.Id)) Erros[$"{h.Id}"] = "Este horário está em uso em alguma grade. Esvazie as células antes de remover";
                    else remover.Add(h);
                    continue;
                }
                linhas.Add((h.Id.ToString(), h));
            }
            for (int i = 0; i < Novos; i++)
            {
                var k = $"n{(int)turno}_{i}";
                if (string.IsNullOrWhiteSpace(f[$"ini_{k}"]) && string.IsNullOrWhiteSpace(f[$"fim_{k}"])) continue;
                linhas.Add((k, new HorarioAula { Turno = turno }));
            }

            var validos = new List<(string Chave, HorarioAula H, TimeOnly Ini, TimeOnly Fim)>();
            foreach (var (chave, h) in linhas)
            {
                if (!RegrasHorario.LerHora(f[$"ini_{chave}"], out var ini) || !RegrasHorario.LerHora(f[$"fim_{chave}"], out var fim))
                { Erros[chave] = "Informe início e fim no formato hh:mm (ex.: 07:30)"; continue; }
                var e = RegrasHorario.ValidarIntervalo(ini, fim);
                if (e != null) { Erros[chave] = e; continue; }
                validos.Add((chave, h, ini, fim));
            }
            foreach (var i in RegrasHorario.Sobrepostos(validos.Select(v => (v.Ini, v.Fim)).ToList()))
                Erros[validos[i].Chave] = "Sobrepõe outro horário do turno";

            foreach (var (chave, h, ini, fim) in validos.Where(v => !Erros.ContainsKey(v.Chave)))
            {
                var inter = f.ContainsKey($"int_{chave}");
                if (h.Id == 0) { h.Inicio = ini; h.Fim = fim; h.Intervalo = inter; novos.Add(h); alteracoes.Add($"+{turno.Nome()} {h.Rotulo}"); }
                else if (h.Inicio != ini || h.Fim != fim || h.Intervalo != inter)
                { alteracoes.Add($"{turno.Nome()} {h.Rotulo} → {ini:HH\\:mm} às {fim:HH\\:mm}"); h.Inicio = ini; h.Fim = fim; h.Intervalo = inter; }
            }
        }
        if (Erros.Count > 0) { _db.ChangeTracker.Clear(); await Carregar(); return Page(); }

        foreach (var r in remover) alteracoes.Add($"-{r.Turno.Nome()} {r.Rotulo}");
        _db.HorariosAula.RemoveRange(remover);
        _db.HorariosAula.AddRange(novos);
        if (alteracoes.Count > 0) _db.Registrar(User, Auditoria.Horarios, "Horários de aula: " + string.Join("; ", alteracoes));
        await _db.SaveChangesAsync();
        Aviso = alteracoes.Count > 0 ? "Horários de aula salvos." : "Nada mudou.";
        return RedirectToPage();
    }
}
