using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Matriculas;

public class EditModel : PageModel
{
    private readonly EscolaContext _db;
    public EditModel(EscolaContext db) => _db = db;

    [BindProperty] public Matricula Matricula { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var m = await _db.Matriculas.FindAsync(id);
        if (m == null) return NotFound();
        Matricula = m;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        Matricula.Validar(ModelState);
        if (!ModelState.IsValid) return Page();

        var m = await _db.Matriculas.FindAsync(id);
        if (m == null) return NotFound();

        Matricula.Normalizar();

        // Atualiza só os campos editáveis (mantém Id e DataMatricula)
        m.AlunoNome = Matricula.AlunoNome;
        m.DataNascimento = Matricula.DataNascimento;
        m.Cpf = Matricula.Cpf;
        m.Serie = Matricula.Serie;
        m.Turno = Matricula.Turno;
        m.AnoLetivo = Matricula.AnoLetivo;
        m.Status = Matricula.Status;
        m.Observacoes = Matricula.Observacoes;
        m.MesmoResponsavel = Matricula.MesmoResponsavel;
        m.Academico.Nome = Matricula.Academico.Nome;
        m.Academico.Cpf = Matricula.Academico.Cpf;
        m.Academico.Telefone = Matricula.Academico.Telefone;
        m.Academico.Email = Matricula.Academico.Email;
        m.Financeiro.Nome = Matricula.Financeiro.Nome;
        m.Financeiro.Cpf = Matricula.Financeiro.Cpf;
        m.Financeiro.Telefone = Matricula.Financeiro.Telefone;
        m.Financeiro.Email = Matricula.Financeiro.Email;

        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
