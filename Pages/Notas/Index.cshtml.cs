using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Notas;

public class IndexModel : PageModel
{
    private readonly EscolaContext _db;
    public IndexModel(EscolaContext db) => _db = db;

    public record Item(int TurmaId, int DisciplinaId, string Turma, int Ano, string Turno, string Disciplina, int Alunos);

    public List<Item> Itens { get; set; } = new();
    public bool TemCadastro { get; set; }

    public async Task OnGetAsync()
    {
        var uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var prof = await _db.Professores.AsNoTracking().FirstOrDefaultAsync(p => p.UsuarioId == uid);
        TemCadastro = prof != null;
        if (prof == null) return;

        var lista = await _db.Set<TurmaDisciplina>().AsNoTracking()
            .Where(x => x.ProfessorId == prof.Id && x.Turma!.Ativa)
            .Select(x => new
            {
                x.TurmaId, x.DisciplinaId, x.Turma!.Serie, x.Turma.Letra, x.Turma.AnoLetivo, x.Turma.Turno,
                Disciplina = x.Disciplina!.Nome,
                Alunos = _db.Matriculas.Count(m => m.TurmaId == x.TurmaId && m.Status == StatusMatricula.Ativa)
            })
            .OrderByDescending(x => x.AnoLetivo).ThenBy(x => x.Serie).ThenBy(x => x.Letra).ThenBy(x => x.Disciplina)
            .ToListAsync();

        Itens = lista.Select(x => new Item(x.TurmaId, x.DisciplinaId, $"{x.Serie!.Value.Nome()} {x.Letra}", x.AnoLetivo,
            x.Turno?.ToString() ?? "", x.Disciplina, x.Alunos)).ToList();
    }
}
