using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Horarios;

// Semana de um professor (só leitura), para a coordenação conferir
public class ProfessorModel : PageModel
{
    private readonly EscolaContext _db;
    public ProfessorModel(EscolaContext db) => _db = db;

    public Data.Professor Professor { get; set; } = null!;
    public List<(Turno Turno, GradeView Grade)> Grades { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var p = await _db.Professores.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();
        Professor = p;
        Grades = await GradesDoProfessor.MontarAsync(_db, id);
        return Page();
    }
}
