using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Desempenho;

public class AlunoModel : PageModel
{
    private readonly EscolaContext _db;
    public AlunoModel(EscolaContext db) => _db = db;

    public Matricula Matricula { get; set; } = null!;
    public ResultadoAluno Resultado { get; set; } = null!;

    // Faltas lançadas pelo professor, para a secretaria abonar (chave "disciplinaId-bimestre")
    public record LinhaFalta(string Chave, string Disciplina, int Bimestre, int Faltas);
    public List<LinhaFalta> Faltas { get; set; } = new();
    [BindProperty] public Dictionary<string, string?> Abono { get; set; } = new();
    [BindProperty] public Dictionary<string, string?> Motivo { get; set; } = new();
    public Dictionary<string, string> Erros { get; set; } = new();
    [TempData] public string? Aviso { get; set; }

    private async Task<bool> Carregar(int id)
    {
        var m = await _db.Matriculas.AsNoTracking().Include(x => x.Aluno).Include(x => x.Turma).FirstOrDefaultAsync(x => x.Id == id);
        if (m == null || m.TurmaId == null) return false;
        Matricula = m;
        Resultado = await ResultadoAlunoDb.CarregarAsync(_db, id, m.TurmaId.Value);
        var nomes = await _db.Set<TurmaDisciplina>().AsNoTracking().Where(x => x.TurmaId == m.TurmaId)
            .Select(x => new { x.DisciplinaId, x.Disciplina!.Nome }).ToDictionaryAsync(x => x.DisciplinaId, x => x.Nome);
        var fs = await _db.Frequencias.AsNoTracking().Where(f => f.MatriculaId == id && f.Faltas > 0)
            .OrderBy(f => f.Bimestre).ToListAsync();
        Faltas = fs.Where(f => nomes.ContainsKey(f.DisciplinaId)).OrderBy(f => nomes[f.DisciplinaId]).ThenBy(f => f.Bimestre)
            .Select(f => new LinhaFalta($"{f.DisciplinaId}-{f.Bimestre}", nomes[f.DisciplinaId], f.Bimestre, f.Faltas)).ToList();
        return true;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await Carregar(id)) return NotFound();
        var reg = await _db.Frequencias.AsNoTracking().Where(f => f.MatriculaId == id && f.Faltas > 0).ToListAsync();
        foreach (var f in reg) { var k = $"{f.DisciplinaId}-{f.Bimestre}"; Abono[k] = f.Abonadas.ToString(); Motivo[k] = f.MotivoAbono; }
        return Page();
    }

    // Abono de faltas (justificativa): não passa das faltas lançadas; com abono, o motivo é obrigatório
    public async Task<IActionResult> OnPostAbonarAsync(int id)
    {
        if (!await Carregar(id)) return NotFound();
        var regs = await _db.Frequencias.Where(f => f.MatriculaId == id && f.Faltas > 0).ToListAsync();
        var novos = new List<(Frequencia F, string Chave, int Abonadas, string Motivo)>();
        foreach (var f in regs)
        {
            var k = $"{f.DisciplinaId}-{f.Bimestre}";
            if (!Abono.ContainsKey(k)) continue; // linha não enviada: não mexe
            var txt = (Abono[k] ?? "").Trim();
            var motivo = (Motivo.GetValueOrDefault(k) ?? "").Trim();
            int n = 0;
            if (txt != "" && (!int.TryParse(txt, out n) || n < 0)) { Erros[k] = "Número inteiro, 0 ou mais"; continue; }
            if (n > f.Faltas) { Erros[k] = $"Maior que as faltas lançadas ({f.Faltas})"; continue; }
            if (n > 0 && motivo.Length < 3) { Erros[k] = "Informe o motivo do abono"; continue; }
            if (motivo.Length > 200) { Erros[k] = "Motivo com no máximo 200 caracteres"; continue; }
            novos.Add((f, k, n, n == 0 ? "" : motivo));
        }
        if (Erros.Count > 0) return Page();

        var mud = new List<string>();
        foreach (var (f, k, n, motivo) in novos)
        {
            if (f.Abonadas == n && f.MotivoAbono == motivo) continue;
            var disc = Faltas.First(x => x.Chave == k);
            mud.Add($"{disc.Disciplina} {disc.Bimestre}º bim.: {f.Abonadas} → {n}" + (n > 0 ? $" ({motivo})" : ""));
            f.Abonadas = n; f.MotivoAbono = motivo;
        }
        if (mud.Count > 0)
            _db.Registrar(User, Auditoria.Frequencia, $"Abono de faltas de {Matricula.Aluno!.Nome} (matrícula {id}): {string.Join("; ", mud)}");
        await _db.SaveChangesAsync();
        Aviso = mud.Count > 0 ? "Abono de faltas salvo. A frequência foi recalculada." : "Nada mudou.";
        return RedirectToPage(new { id });
    }
}
