using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.SalaDeAula;

// Professor: lista as turmas/disciplinas dele. Aluno e responsável: leem o mural da turma do aluno.
public class IndexModel : PageModel
{
    private readonly EscolaContext _db;
    public IndexModel(EscolaContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public int? AlunoId { get; set; }
    [BindProperty(SupportsGet = true)] public int? DisciplinaId { get; set; }

    public bool EhProfessor { get; set; }

    // Professor
    public record Atribuicao(int TurmaId, int DisciplinaId, string Turma, int Ano, string Disciplina, int Publicacoes);
    public List<Atribuicao> Atribuicoes { get; set; } = new();

    // Aluno / responsável
    public List<Aluno> Alunos { get; set; } = new();
    public Aluno? Aluno { get; set; }
    public Matricula? Matricula { get; set; }
    public List<(int Id, string Nome)> Disciplinas { get; set; } = new();
    public List<Publicacao> Itens { get; set; } = new();

    public async Task OnGetAsync()
    {
        if (User.IsInRole("Professor")) { EhProfessor = true; await CarregarProfessor(); return; }

        Alunos = await Acessos.AlunosDoUsuario(_db, User);
        Aluno = Alunos.FirstOrDefault(a => a.Id == AlunoId) ?? Alunos.FirstOrDefault();
        if (Aluno == null) return;

        Matricula = await _db.Matriculas.AsNoTracking().Include(m => m.Turma)
            .Where(m => m.AlunoId == Aluno.Id && m.TurmaId != null)
            .OrderByDescending(m => m.AnoLetivo).FirstOrDefaultAsync();
        if (Matricula == null) return;

        Disciplinas = (await _db.Set<TurmaDisciplina>().AsNoTracking().Where(x => x.TurmaId == Matricula.TurmaId)
            .Select(x => new { x.DisciplinaId, x.Disciplina!.Nome }).OrderBy(x => x.Nome).ToListAsync())
            .Select(x => (x.DisciplinaId, x.Nome)).ToList();

        var q = _db.Publicacoes.AsNoTracking().Include(p => p.Disciplina).Include(p => p.Professor)
            .Where(p => p.TurmaId == Matricula.TurmaId);
        if (DisciplinaId != null) q = q.Where(p => p.DisciplinaId == DisciplinaId);
        Itens = await q.OrderByDescending(p => p.CriadoEm).Take(100).ToListAsync();
    }

    private async Task CarregarProfessor()
    {
        var uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var prof = await _db.Professores.AsNoTracking().FirstOrDefaultAsync(p => p.UsuarioId == uid && p.Ativo);
        if (prof == null) return;
        var lista = await _db.Set<TurmaDisciplina>().AsNoTracking()
            .Where(x => x.ProfessorId == prof.Id && x.Turma!.Ativa)
            .OrderByDescending(x => x.Turma!.AnoLetivo).ThenBy(x => x.Turma!.Serie).ThenBy(x => x.Turma!.Letra).ThenBy(x => x.Disciplina!.Nome)
            .Select(x => new
            {
                x.TurmaId, x.DisciplinaId, Turma = x.Turma!, Disciplina = x.Disciplina!.Nome,
                N = _db.Publicacoes.Count(p => p.TurmaId == x.TurmaId && p.DisciplinaId == x.DisciplinaId)
            }).ToListAsync();
        Atribuicoes = lista.Select(x => new Atribuicao(x.TurmaId, x.DisciplinaId, x.Turma.Nome, x.Turma.AnoLetivo, x.Disciplina, x.N)).ToList();
    }
}
