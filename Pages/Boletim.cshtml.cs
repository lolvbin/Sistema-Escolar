using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages;

public class BoletimModel : PageModel
{
    private readonly EscolaContext _db;
    public BoletimModel(EscolaContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public int? AlunoId { get; set; }

    public List<Aluno> Alunos { get; set; } = new();
    public Aluno? Aluno { get; set; }
    public Matricula? Matricula { get; set; }

    public Situacao Geral { get; set; }
    public decimal? FreqGeral { get; set; }
    public int FaltasAbonadas { get; set; }
    public List<LinhaDisciplina> Linhas { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        Alunos = await Acessos.AlunosDoUsuario(_db, User);

        // Só enxerga aluno da própria lista
        Aluno = Alunos.FirstOrDefault(a => a.Id == AlunoId) ?? Alunos.FirstOrDefault();
        if (Aluno == null) return Page();

        Matricula = await _db.Matriculas.AsNoTracking().Include(m => m.Turma)
            .Where(m => m.AlunoId == Aluno.Id && m.TurmaId != null)
            .OrderByDescending(m => m.AnoLetivo).FirstOrDefaultAsync();
        if (Matricula == null) return Page();

        var discs = await _db.Set<TurmaDisciplina>().AsNoTracking()
            .Where(x => x.TurmaId == Matricula.TurmaId)
            .Select(x => new { x.DisciplinaId, x.Disciplina!.Nome }).OrderBy(x => x.Nome).ToListAsync();
        var notas = await _db.Notas.AsNoTracking().Where(n => n.MatriculaId == Matricula.Id).ToListAsync();
        var faltas = await _db.Frequencias.AsNoTracking().Where(f => f.MatriculaId == Matricula.Id).ToListAsync();
        var aulas = await _db.Aulas.AsNoTracking().Where(a => a.TurmaId == Matricula.TurmaId).ToListAsync();

        var r = ResultadoAluno.Calcular(discs.Select(d => (d.DisciplinaId, d.Nome)), notas, faltas, aulas);
        Linhas = r.Linhas;
        FreqGeral = r.FreqGeral;
        FaltasAbonadas = r.FaltasAbonadas;
        Geral = r.Geral;
        return Page();
    }
}
