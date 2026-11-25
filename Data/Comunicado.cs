using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace SistemaEscolar.Data;

public enum PublicoComunicado
{
    [Display(Name = "Todos")] Todos,
    [Display(Name = "Responsáveis")] Responsaveis,
    [Display(Name = "Alunos")] Alunos,
    [Display(Name = "Professores")] Professores,
    [Display(Name = "Funcionários")] Funcionarios
}

// Comunicado da escola (mural da tela inicial): vale até a data de validade e depois só aparece no histórico dos funcionários.
public class Comunicado
{
    public int Id { get; set; }
    public string Titulo { get; set; } = "";
    public string? Texto { get; set; }
    public string? Link { get; set; }                 // só http/https
    public PublicoComunicado Publico { get; set; }
    public int? TurmaId { get; set; }                 // opcional: só quem é dessa turma (alunos, responsáveis, professores dela)
    public DateTime ValidoAte { get; set; }           // data (inclusive)
    public bool Fixado { get; set; }
    public int? AutorId { get; set; }
    public string AutorNome { get; set; } = "";
    public DateTime CriadoEm { get; set; }
    public DateTime? EditadoEm { get; set; }
    public Turma? Turma { get; set; }

    public bool Vigente(DateTime hoje) => ValidoAte.Date >= hoje.Date;

    /// <summary>Se o comunicado aparece no mural de quem tem esse perfil (e essas turmas).</summary>
    public bool VisivelPara(Perfil perfil, IReadOnlyCollection<int> turmasDoUsuario, DateTime hoje)
    {
        if (!Vigente(hoje)) return false;
        var publicoOk = Publico switch
        {
            PublicoComunicado.Todos => true,
            PublicoComunicado.Responsaveis => perfil == Perfil.Responsavel,
            PublicoComunicado.Alunos => perfil == Perfil.Aluno,
            PublicoComunicado.Professores => perfil == Perfil.Professor,
            _ => perfil == Perfil.Funcionario,
        };
        if (!publicoOk) return false;
        return TurmaId == null || perfil == Perfil.Funcionario || turmasDoUsuario.Contains(TurmaId.Value);
    }
}

public static class ComunicadoExt
{
    public static string Nome(this PublicoComunicado p) => p switch
    {
        PublicoComunicado.Todos => "Todos",
        PublicoComunicado.Responsaveis => "Responsáveis",
        PublicoComunicado.Alunos => "Alunos",
        PublicoComunicado.Professores => "Professores",
        _ => "Funcionários",
    };
}

public static class MuralComunicados
{
    public const int Limite = 10;

    /// <summary>Turmas de quem está logado (para comunicados por turma): a dos filhos/aluno (matrícula ativa do ano) ou as que o professor leciona.</summary>
    public static async Task<List<int>> TurmasDoUsuarioAsync(EscolaContext db, System.Security.Claims.ClaimsPrincipal user, Perfil perfil)
    {
        var uid = int.Parse(user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        if (perfil == Perfil.Professor)
            return await db.Set<TurmaDisciplina>().AsNoTracking().Where(x => x.Professor!.UsuarioId == uid).Select(x => x.TurmaId).Distinct().ToListAsync();
        if (perfil == Perfil.Aluno || perfil == Perfil.Responsavel)
        {
            var alunos = (await Acessos.AlunosDoUsuario(db, user)).Select(a => a.Id).ToList();
            var ano = DateTime.Today.Year;
            return await db.Matriculas.AsNoTracking()
                .Where(m => alunos.Contains(m.AlunoId ?? 0) && m.TurmaId != null && m.Status == StatusMatricula.Ativa && m.AnoLetivo == ano)
                .Select(m => m.TurmaId!.Value).Distinct().ToListAsync();
        }
        return new List<int>();
    }

    /// <summary>Comunicados vigentes que o usuário deve ver: fixados primeiro, depois os mais novos.</summary>
    public static async Task<List<Comunicado>> DoUsuarioAsync(EscolaContext db, System.Security.Claims.ClaimsPrincipal user, Perfil perfil)
    {
        var hoje = DateTime.Today;
        var turmas = await TurmasDoUsuarioAsync(db, user, perfil);
        var vigentes = await db.Comunicados.AsNoTracking().Where(c => c.ValidoAte >= hoje)
            .OrderByDescending(c => c.Fixado).ThenByDescending(c => c.CriadoEm).Take(200).ToListAsync();
        return vigentes.Where(c => c.VisivelPara(perfil, turmas, hoje)).Take(Limite).ToList();
    }
}
