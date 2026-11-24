using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace SistemaEscolar.Data;

public enum TipoPublicacao
{
    [Display(Name = "Aviso")] Aviso,
    [Display(Name = "Material")] Material,
    [Display(Name = "Atividade")] Atividade
}

// Publicação da Sala de aula: o professor publica para uma turma, em uma disciplina sua.
// Aluno e responsável só leem. Atividade tem prazo; não há envio de arquivos nem entrega.
public class Publicacao
{
    public int Id { get; set; }
    public int TurmaId { get; set; }
    public int DisciplinaId { get; set; }
    public int ProfessorId { get; set; }
    public TipoPublicacao Tipo { get; set; }
    public string Titulo { get; set; } = "";
    public string? Texto { get; set; }
    public string? Link { get; set; }           // só http/https (validado ao salvar)
    public DateTime? Prazo { get; set; }        // só para Atividade
    public DateTime CriadoEm { get; set; }

    public Disciplina? Disciplina { get; set; }
    public Professor? Professor { get; set; }

    public bool Encerrada => Tipo == TipoPublicacao.Atividade && Prazo != null && Prazo.Value.Date < DateTime.Today;

    /// <summary>Link aceito: URL absoluta http ou https, até 300 caracteres.</summary>
    public static bool LinkValido(string? link) =>
        link != null && link.Length <= 300 && Uri.TryCreate(link, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);
}

public static class Acessos
{
    /// <summary>Alunos que o usuário logado pode ver: o próprio (perfil Aluno) ou os ligados ao e-mail dele (Responsável).</summary>
    public static async Task<List<Aluno>> AlunosDoUsuario(EscolaContext db, ClaimsPrincipal user)
    {
        var uid = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (user.IsInRole("Aluno"))
            return await db.Alunos.AsNoTracking().Where(a => a.UsuarioId == uid).ToListAsync();
        var email = (user.FindFirstValue(ClaimTypes.Email) ?? "").ToLower();
        return await db.Alunos.AsNoTracking()
            .Where(a => a.Academico.Email.ToLower() == email || a.Financeiro.Email.ToLower() == email)
            .OrderBy(a => a.Nome).ToListAsync();
    }
}
