using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Turmas;

public class DetailsModel : PageModel
{
    private readonly EscolaContext _db;
    public DetailsModel(EscolaContext db) => _db = db;

    public Turma Turma { get; set; } = new();
    public List<Matricula> Alunos { get; set; } = new();
    public List<Matricula> Candidatos { get; set; } = new();
    public List<Professor> ProfessoresAtivos { get; set; } = new();
    public record LinhaDisc(Disciplina Disciplina, int? ProfessorId);
    public List<LinhaDisc> Linhas { get; set; } = new();

    [BindProperty] public Dictionary<int, int?> Professores { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var t = await _db.Turmas.AsNoTracking().Include(x => x.Disciplinas).FirstOrDefaultAsync(x => x.Id == id);
        if (t == null) return NotFound();
        Turma = t;

        Alunos = await _db.Matriculas.AsNoTracking().Include(m => m.Aluno)
            .Where(m => m.TurmaId == id).OrderBy(m => m.Aluno!.Nome).ToListAsync();

        // Só matrícula Ativa, mesma série/ano/turno, ainda sem turma
        Candidatos = await _db.Matriculas.AsNoTracking().Include(m => m.Aluno)
            .Where(m => m.TurmaId == null && m.Status == StatusMatricula.Ativa
                     && m.Serie == t.Serie && m.AnoLetivo == t.AnoLetivo && m.Turno == t.Turno)
            .OrderBy(m => m.Aluno!.Nome).ToListAsync();

        ProfessoresAtivos = await _db.Professores.AsNoTracking().Where(p => p.Ativo).OrderBy(p => p.Nome).ToListAsync();

        var discs = await _db.Disciplinas.AsNoTracking()
            .Where(d => d.Ativa && d.Series.Any(s => s.Serie == t.Serie)).OrderBy(d => d.Nome).ToListAsync();
        Linhas = discs.Select(d => new LinhaDisc(d, t.Disciplinas.FirstOrDefault(x => x.DisciplinaId == d.Id)?.ProfessorId)).ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostAdicionarAsync(int id, List<int> matriculaIds)
    {
        var t = await _db.Turmas.FindAsync(id);
        if (t == null) return NotFound();
        if (!t.Ativa) { TempData["Erro"] = "Turma inativa."; return RedirectToPage(new { id }); }

        var ms = await _db.Matriculas.Where(m => matriculaIds.Contains(m.Id) && m.TurmaId == null
                    && m.Status == StatusMatricula.Ativa && m.Serie == t.Serie
                    && m.AnoLetivo == t.AnoLetivo && m.Turno == t.Turno).ToListAsync();
        foreach (var m in ms) m.TurmaId = id;
        await _db.SaveChangesAsync();

        TempData["Aviso"] = $"{ms.Count} aluno(s) adicionado(s).";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRemoverAsync(int id, int matriculaId)
    {
        var m = await _db.Matriculas.FirstOrDefaultAsync(x => x.Id == matriculaId && x.TurmaId == id);
        if (m != null) { m.TurmaId = null; await _db.SaveChangesAsync(); }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostProfessoresAsync(int id)
    {
        var t = await _db.Turmas.Include(x => x.Disciplinas).FirstOrDefaultAsync(x => x.Id == id);
        if (t == null) return NotFound();

        var discOk = (await _db.Disciplinas.Where(d => d.Ativa && d.Series.Any(s => s.Serie == t.Serie))
            .Select(d => d.Id).ToListAsync()).ToHashSet();
        var profOk = (await _db.Professores.Where(p => p.Ativo).Select(p => p.Id).ToListAsync()).ToHashSet();

        foreach (var (discId, profId) in Professores)
        {
            if (!discOk.Contains(discId)) continue;
            var atual = t.Disciplinas.FirstOrDefault(x => x.DisciplinaId == discId);
            if (profId == null)
            {
                if (atual != null) _db.Remove(atual);
            }
            else if (profOk.Contains(profId.Value))
            {
                if (atual == null) t.Disciplinas.Add(new TurmaDisciplina { DisciplinaId = discId, ProfessorId = profId });
                else atual.ProfessorId = profId;
            }
        }
        await _db.SaveChangesAsync();
        TempData["Aviso"] = "Professores salvos.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostAlternarAsync(int id)
    {
        var t = await _db.Turmas.FindAsync(id);
        if (t == null) return NotFound();
        t.Ativa = !t.Ativa;
        await _db.SaveChangesAsync();
        return RedirectToPage(new { id });
    }

    // Só exclui turma vazia (sem alunos). Os professores atribuídos saem junto.
    public async Task<IActionResult> OnPostExcluirAsync(int id)
    {
        if (await _db.Matriculas.AnyAsync(m => m.TurmaId == id))
        {
            TempData["Erro"] = "Remova os alunos antes de excluir a turma.";
            return RedirectToPage(new { id });
        }
        var t = await _db.Turmas.FindAsync(id);
        if (t != null) { _db.Turmas.Remove(t); await _db.SaveChangesAsync(); }
        TempData["Aviso"] = "Turma excluída.";
        return RedirectToPage("Index");
    }
}
