using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Matriculas;

public class CreateModel : PageModel
{
    private readonly EscolaContext _db;
    public CreateModel(EscolaContext db) => _db = db;

    [BindProperty] public Matricula Matricula { get; set; } = new();

    public async Task OnGetAsync() => await CarregarAlunos();

    public async Task<IActionResult> OnPostAsync()
    {
        Matricula.Validar(ModelState);

        if (Matricula.AlunoId != null && ModelState.IsValid)
        {
            if (await _db.Matriculas.AnyAsync(m => m.AlunoId == Matricula.AlunoId && m.AnoLetivo == Matricula.AnoLetivo))
                ModelState.AddModelError("AlunoId", $"Este aluno já tem matrícula em {Matricula.AnoLetivo}");
        }

        if (!ModelState.IsValid)
        {
            await CarregarAlunos();
            return Page();
        }

        // Toda matrícula nasce pendente (liberação virá do financeiro/funcionário).
        Matricula.Status = StatusMatricula.Pendente;
        Matricula.DataMatricula = DateTime.Today;
        Matricula.Observacoes = Matricula.Observacoes?.Trim();

        _db.Matriculas.Add(Matricula);
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }

    private async Task CarregarAlunos()
    {
        var alunos = await _db.Alunos.AsNoTracking().OrderBy(a => a.Nome)
            .Select(a => new SelectListItem(a.Nome + " (" + a.Cpf + ")", a.Id.ToString()))
            .ToListAsync();
        ViewData["Alunos"] = alunos;
    }
}
