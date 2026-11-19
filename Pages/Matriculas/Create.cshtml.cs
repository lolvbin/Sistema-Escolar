using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Matriculas;

// A matrícula é a porta de entrada: o aluno é criado junto com ela.
// Se o CPF já existe (rematrícula em outro ano), reaproveita o aluno e atualiza os dados dele.
public class CreateModel : PageModel
{
    private readonly EscolaContext _db;
    public CreateModel(EscolaContext db) => _db = db;

    [BindProperty] public Aluno Aluno { get; set; } = new();
    [BindProperty] public Matricula Matricula { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        Aluno.Validar(ModelState, "Aluno");
        Matricula.Validar(ModelState, "Matricula");
        if (!ModelState.IsValid) return Page();

        Aluno.Normalizar();

        var existente = await _db.Alunos.FirstOrDefaultAsync(a => a.Cpf == Aluno.Cpf);
        if (existente == null)
        {
            _db.Alunos.Add(Aluno);
            Matricula.Aluno = Aluno;
        }
        else
        {
            if (!string.Equals(existente.Nome.Trim(), Aluno.Nome, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("Aluno.Cpf", $"Este CPF já pertence a outro aluno ({existente.Nome})");
                return Page();
            }
            if (await _db.Matriculas.AnyAsync(m => m.AlunoId == existente.Id && m.AnoLetivo == Matricula.AnoLetivo))
            {
                ModelState.AddModelError("Matricula.AnoLetivo", $"Este aluno já tem matrícula em {Matricula.AnoLetivo}");
                return Page();
            }
            existente.AtualizarDe(Aluno);
            Matricula.AlunoId = existente.Id;
        }

        // Toda matrícula nasce pendente (liberação virá do financeiro/funcionário).
        Matricula.Status = StatusMatricula.Pendente;
        Matricula.DataMatricula = DateTime.Today;
        Matricula.Observacoes = Matricula.Observacoes?.Trim();

        _db.Matriculas.Add(Matricula);
        await _db.SaveChangesAsync(); // aluno + matrícula na mesma transação
        return RedirectToPage("Index");
    }
}
