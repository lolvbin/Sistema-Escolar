using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Notas;

public class LancarModel : PageModel
{
    private readonly EscolaContext _db;
    public LancarModel(EscolaContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public int TurmaId { get; set; }
    [BindProperty(SupportsGet = true)] public int DisciplinaId { get; set; }
    [BindProperty(SupportsGet = true)] public int Bim { get; set; } = 1;

    // Texto digitado por matrícula (aceita vírgula ou ponto)
    [BindProperty] public Dictionary<int, string?> Valores { get; set; } = new();

    public Turma Turma { get; set; } = null!;
    public Disciplina Disciplina { get; set; } = null!;
    public List<Matricula> Alunos { get; set; } = new();
    public Dictionary<int, string> Erros { get; set; } = new();

    [TempData] public string? Aviso { get; set; }

    // Confere se o professor logado é o da disciplina nesta turma e carrega os dados
    private async Task<IActionResult?> Carregar()
    {
        if (Bim < 1 || Bim > TabelaNotas.Bimestres) return NotFound();
        var uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var prof = await _db.Professores.AsNoTracking().FirstOrDefaultAsync(p => p.UsuarioId == uid && p.Ativo);
        if (prof == null) return Forbid();

        var td = await _db.Set<TurmaDisciplina>().AsNoTracking()
            .Include(x => x.Turma).Include(x => x.Disciplina)
            .FirstOrDefaultAsync(x => x.TurmaId == TurmaId && x.DisciplinaId == DisciplinaId);
        if (td == null) return NotFound();
        if (td.ProfessorId != prof.Id) return Forbid();

        Turma = td.Turma!;
        Disciplina = td.Disciplina!;
        Alunos = await _db.Matriculas.AsNoTracking().Include(m => m.Aluno)
            .Where(m => m.TurmaId == TurmaId && m.Status == StatusMatricula.Ativa)
            .OrderBy(m => m.Aluno!.Nome).ToListAsync();
        return null;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var r = await Carregar();
        if (r != null) return r;

        var ids = Alunos.Select(a => a.Id).ToList();
        var notas = await _db.Notas.AsNoTracking()
            .Where(n => n.DisciplinaId == DisciplinaId && n.Bimestre == Bim && ids.Contains(n.MatriculaId))
            .ToListAsync();
        Valores = notas.ToDictionary(n => n.MatriculaId, n => n.Valor.ToString("0.0", CultureInfo.CurrentCulture));
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var r = await Carregar();
        if (r != null) return r;

        // Valida tudo antes de gravar: se algum valor estiver errado, nada é salvo
        var novos = new Dictionary<int, decimal?>();
        foreach (var m in Alunos)
        {
            var txt = (Valores.GetValueOrDefault(m.Id) ?? "").Trim();
            if (txt == "") { novos[m.Id] = null; continue; }
            if (!decimal.TryParse(txt.Replace('.', ','), NumberStyles.Number, CultureInfo.CurrentCulture, out var v)
                || v < 0 || v > 10)
            {
                Erros[m.Id] = "Use um número de 0 a 10";
                continue;
            }
            novos[m.Id] = Math.Round(v, 1, MidpointRounding.AwayFromZero);
        }
        if (Erros.Count > 0) return Page();

        var ids = Alunos.Select(a => a.Id).ToList();
        var atuais = await _db.Notas
            .Where(n => n.DisciplinaId == DisciplinaId && n.Bimestre == Bim && ids.Contains(n.MatriculaId))
            .ToDictionaryAsync(n => n.MatriculaId);

        foreach (var (mid, valor) in novos)
        {
            atuais.TryGetValue(mid, out var n);
            if (valor == null) { if (n != null) _db.Notas.Remove(n); }
            else if (n == null) _db.Notas.Add(new Nota { MatriculaId = mid, DisciplinaId = DisciplinaId, Bimestre = Bim, Valor = valor.Value });
            else n.Valor = valor.Value;
        }
        await _db.SaveChangesAsync();

        Aviso = $"Notas do {Bim}º bimestre salvas.";
        return RedirectToPage(new { TurmaId, DisciplinaId, Bim });
    }
}
