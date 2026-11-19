using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Matriculas;

public class EditModel : PageModel
{
    private readonly EscolaContext _db;
    public EditModel(EscolaContext db) => _db = db;

    [BindProperty] public Matricula Matricula { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var m = await _db.Matriculas.Include(x => x.Aluno).FirstOrDefaultAsync(x => x.Id == id);
        if (m == null) return NotFound();
        Matricula = m;
        ViewData["AlunoNome"] = m.Aluno?.Nome;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var m = await _db.Matriculas.Include(x => x.Aluno).FirstOrDefaultAsync(x => x.Id == id);
        if (m == null) return NotFound();
        ViewData["AlunoNome"] = m.Aluno?.Nome;

        Matricula.Validar(ModelState);
        if (ModelState.IsValid
            && await _db.Matriculas.AnyAsync(x => x.Id != id && x.AlunoId == m.AlunoId && x.AnoLetivo == Matricula.AnoLetivo))
            ModelState.AddModelError("AnoLetivo", $"Este aluno já tem matrícula em {Matricula.AnoLetivo}");
        if (!ModelState.IsValid) return Page();

        // Atualiza só os campos editáveis (o aluno, o Id e a DataMatricula não mudam)
        m.Serie = Matricula.Serie;
        m.Turno = Matricula.Turno;
        m.AnoLetivo = Matricula.AnoLetivo;
        m.Status = Matricula.Status;
        m.Observacoes = Matricula.Observacoes?.Trim();

        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
