using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Alunos;

public class EditModel : PageModel
{
    private readonly EscolaContext _db;
    public EditModel(EscolaContext db) => _db = db;

    [BindProperty] public Aluno Aluno { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var a = await _db.Alunos.FindAsync(id);
        if (a == null) return NotFound();
        Aluno = a;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        Aluno.Validar(ModelState);
        if (!ModelState.IsValid) return Page();

        var a = await _db.Alunos.FindAsync(id);
        if (a == null) return NotFound();

        Aluno.Normalizar();
        if (await _db.Alunos.AnyAsync(x => x.Cpf == Aluno.Cpf && x.Id != id))
        {
            ModelState.AddModelError("Cpf", "Já existe um aluno com este CPF");
            return Page();
        }

        a.Nome = Aluno.Nome;
        a.DataNascimento = Aluno.DataNascimento;
        a.Cpf = Aluno.Cpf;
        a.Observacoes = Aluno.Observacoes;
        a.MesmoResponsavel = Aluno.MesmoResponsavel;
        a.Academico.Nome = Aluno.Academico.Nome;
        a.Academico.Cpf = Aluno.Academico.Cpf;
        a.Academico.Telefone = Aluno.Academico.Telefone;
        a.Academico.Email = Aluno.Academico.Email;
        a.Financeiro.Nome = Aluno.Financeiro.Nome;
        a.Financeiro.Cpf = Aluno.Financeiro.Cpf;
        a.Financeiro.Telefone = Aluno.Financeiro.Telefone;
        a.Financeiro.Email = Aluno.Financeiro.Email;

        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
