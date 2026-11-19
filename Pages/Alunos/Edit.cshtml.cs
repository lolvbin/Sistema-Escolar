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

    public string? AcessoAluno { get; set; }          // e-mail do login do aluno, se existir
    public Dictionary<string, bool> AcessoResp { get; set; } = new(); // e-mail do responsável -> já tem login?
    [TempData] public string? Aviso { get; set; }
    [TempData] public string? Erro { get; set; }

    private async Task CarregarAcessos(Aluno a)
    {
        if (a.UsuarioId != null)
            AcessoAluno = await _db.Usuarios.Where(u => u.Id == a.UsuarioId).Select(u => u.Email).FirstOrDefaultAsync();
        foreach (var e in new[] { a.Academico.Email, a.Financeiro.Email }.Select(x => x.Trim().ToLower()).Distinct())
            AcessoResp[e] = await _db.Usuarios.AnyAsync(u => u.Email == e);
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var a = await _db.Alunos.FindAsync(id);
        if (a == null) return NotFound();
        Aluno = a;
        await CarregarAcessos(a);
        return Page();
    }

    // Cria o login do aluno (e-mail informado) ou do responsável (e-mail já cadastrado nele). Senha provisória mostrada uma vez.
    public async Task<IActionResult> OnPostAcessoAsync(int id, string tipo, string? email)
    {
        var a = await _db.Alunos.FindAsync(id);
        if (a == null) return NotFound();

        string nome; Perfil perfil;
        if (tipo == "aluno")
        {
            if (a.UsuarioId != null) { Erro = "O aluno já tem acesso."; return RedirectToPage(new { id }); }
            email = (email ?? "").Trim().ToLowerInvariant();
            if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
            { Erro = "Informe um e-mail válido para o aluno."; return RedirectToPage(new { id }); }
            nome = a.Nome; perfil = Perfil.Aluno;
        }
        else
        {
            email = (email ?? "").Trim().ToLowerInvariant();
            var r = new[] { a.Academico, a.Financeiro }.FirstOrDefault(x => x.Email.Trim().ToLower() == email);
            if (r == null) { Erro = "E-mail não é de um responsável deste aluno."; return RedirectToPage(new { id }); }
            nome = r.Nome; perfil = Perfil.Responsavel;
        }

        if (await _db.Usuarios.AnyAsync(u => u.Email == email))
        { Erro = "Já existe um acesso com este e-mail."; return RedirectToPage(new { id }); }

        var senha = Senha.Provisoria();
        var u = new Usuario { Nome = nome, Email = email, Perfil = perfil, SenhaHash = Senha.Gerar(senha) };
        _db.Usuarios.Add(u);
        await _db.SaveChangesAsync();
        if (perfil == Perfil.Aluno) { a.UsuarioId = u.Id; await _db.SaveChangesAsync(); }

        Aviso = $"Acesso criado: {email}. Senha provisória (anote, só aparece agora): {senha}";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        Aluno.Validar(ModelState);
        if (!ModelState.IsValid) { var x = await _db.Alunos.FindAsync(id); if (x != null) await CarregarAcessos(x); return Page(); }

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
