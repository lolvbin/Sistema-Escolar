using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages;

// Semana do professor logado
public class MeuHorarioModel : PageModel
{
    private readonly EscolaContext _db;
    public MeuHorarioModel(EscolaContext db) => _db = db;

    public Professor? Professor { get; set; }
    public List<(Turno Turno, GradeView Grade)> Grades { get; set; } = new();

    public async Task OnGetAsync()
    {
        var uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        Professor = await _db.Professores.AsNoTracking().FirstOrDefaultAsync(p => p.UsuarioId == uid && p.Ativo);
        if (Professor != null) Grades = await GradesDoProfessor.MontarAsync(_db, Professor.Id);
    }
}
