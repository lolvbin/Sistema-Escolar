using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages;

// Horário da turma do aluno (aluno e responsável, só leitura)
public class HorarioModel : PageModel
{
    private readonly EscolaContext _db;
    public HorarioModel(EscolaContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public int? AlunoId { get; set; }
    public List<Aluno> Alunos { get; set; } = new();
    public Aluno? Aluno { get; set; }
    public Data.Turma? Turma { get; set; }
    public GradeView? Grade { get; set; }

    public async Task OnGetAsync()
    {
        Alunos = await Acessos.AlunosDoUsuario(_db, User);
        Aluno = Alunos.FirstOrDefault(a => a.Id == AlunoId) ?? Alunos.FirstOrDefault();   // só enxerga aluno da própria lista
        if (Aluno == null) return;

        var m = await _db.Matriculas.AsNoTracking().Include(x => x.Turma)
            .Where(x => x.AlunoId == Aluno.Id && x.TurmaId != null && x.Status == StatusMatricula.Ativa)
            .OrderByDescending(x => x.AnoLetivo).FirstOrDefaultAsync();
        Turma = m?.Turma;
        if (Turma == null) return;

        var horarios = await _db.HorariosAula.AsNoTracking().Where(h => h.Turno == Turma.Turno).OrderBy(h => h.Inicio).ToListAsync();
        var aulas = await (from g in _db.Grade.AsNoTracking()
                           join td in _db.Set<TurmaDisciplina>().AsNoTracking().Include(x => x.Professor) on new { g.TurmaId, g.DisciplinaId } equals new { td.TurmaId, td.DisciplinaId }
                           where g.TurmaId == Turma.Id
                           select new { g.DiaSemana, g.HorarioAulaId, Disciplina = g.Disciplina!.Nome, Professor = td.Professor != null ? td.Professor.Nome : null }).ToListAsync();
        Grade = new GradeView { Horarios = horarios };
        foreach (var a in aulas)
            Grade.Celulas[(a.DiaSemana, a.HorarioAulaId)] = a.Professor == null ? new[] { a.Disciplina } : new[] { a.Disciplina, a.Professor };
    }
}
