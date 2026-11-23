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

    public record Linha(string Disciplina, decimal?[] Notas, decimal? Rec, decimal? Media, decimal? Freq, Situacao Situacao);
    public Situacao Geral { get; set; }
    public decimal? FreqGeral { get; set; }
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
        var faltas = await _db.Frequencias.AsNoTracking().Where(f => f.MatriculaId == Matricula.Id).ToListAsync();
        var aulas = await _db.Aulas.AsNoTracking().Where(a => a.TurmaId == Matricula.TurmaId).ToListAsync();

        foreach (var d in discs)
        {
            var v = new decimal?[TabelaNotas.Bimestres];
            foreach (var n in notas.Where(n => n.DisciplinaId == d.DisciplinaId && n.Bimestre >= 1 && n.Bimestre <= v.Length))
                v[n.Bimestre - 1] = n.Valor;
            var rec = notas.FirstOrDefault(n => n.DisciplinaId == d.DisciplinaId && n.Bimestre == TabelaNotas.BimRecuperacao)?.Valor;
            var nf = faltas.Where(f => f.DisciplinaId == d.DisciplinaId).Sum(f => f.Faltas);
            var na = aulas.Where(a => a.DisciplinaId == d.DisciplinaId).Sum(a => a.Aulas);
            Linhas.Add(new Linha(d.Nome, v, rec, Aprovacao.MediaFinal(v, rec), Aprovacao.Frequencia(nf, na), Aprovacao.DaDisciplina(v, rec)));
        }
        FreqGeral = Aprovacao.Frequencia(faltas.Sum(f => f.Faltas), aulas.Sum(a => a.Aulas));
        Geral = Aprovacao.Geral(Linhas.Select(l => l.Situacao), FreqGeral);
        return Page();
    }
}
