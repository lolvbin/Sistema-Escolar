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

    public record Linha(string Disciplina, decimal?[] Notas, decimal? Media, Situacao Situacao);
    public Situacao Geral { get; set; }
    public List<Linha> Linhas { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (User.IsInRole("Aluno"))
            Alunos = await _db.Alunos.AsNoTracking().Where(a => a.UsuarioId == uid).ToListAsync();
        else
        {
            // Responsável: alunos em que o e-mail dele consta como responsável acadêmico ou financeiro
            var email = (User.FindFirstValue(ClaimTypes.Email) ?? "").ToLower();
            Alunos = await _db.Alunos.AsNoTracking()
                .Where(a => a.Academico.Email.ToLower() == email || a.Financeiro.Email.ToLower() == email)
                .OrderBy(a => a.Nome).ToListAsync();
        }

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

        foreach (var d in discs)
        {
            var v = new decimal?[TabelaNotas.Bimestres];
            foreach (var n in notas.Where(n => n.DisciplinaId == d.DisciplinaId && n.Bimestre >= 1 && n.Bimestre <= v.Length))
                v[n.Bimestre - 1] = n.Valor;
            Linhas.Add(new Linha(d.Nome, v, Aprovacao.Media(v), Aprovacao.DaDisciplina(v)));
        }
        Geral = Aprovacao.Geral(Linhas.Select(l => l.Situacao));
        return Page();
    }
}
