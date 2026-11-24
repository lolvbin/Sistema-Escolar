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

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var m = await _db.Matriculas.AsNoTracking().Include(x => x.Aluno).Include(x => x.Turma).FirstOrDefaultAsync(x => x.Id == id);
        if (m == null || m.TurmaId == null) return NotFound();
        Matricula = m;

        Resultado = await ResultadoAlunoDb.CarregarAsync(_db, id, m.TurmaId.Value);
        return Page();
    }
}
