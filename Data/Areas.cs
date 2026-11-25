using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Reflection;

namespace SistemaEscolar.Data;

// Áreas do sistema que um funcionário pode acessar. Acesso por área é "tudo ou nada" (não há só leitura).
[Flags]
public enum Area
{
    Nenhuma = 0,
    [Display(Name = "Matrículas")] Matriculas = 1,
    [Display(Name = "Alunos")] Alunos = 2,
    [Display(Name = "Professores")] Professores = 4,
    [Display(Name = "Disciplinas")] Disciplinas = 8,
    [Display(Name = "Turmas")] Turmas = 16,
    [Display(Name = "Notas e frequência (consulta)")] Desempenho = 32,
    [Display(Name = "Financeiro")] Financeiro = 64,
    [Display(Name = "Calendário (cadastrar eventos)")] Calendario = 128,
    [Display(Name = "Configurações")] Configuracoes = 256,
    [Display(Name = "Usuários (funcionários)")] Usuarios = 512,
    [Display(Name = "Comunicados (mural)")] Comunicados = 1024
}

// Cargos são atalhos: preenchem as áreas típicas ao criar o funcionário (depois dá para ajustar uma a uma).
public enum Cargo
{
    [Display(Name = "Administrador / TI")] Administrador,
    [Display(Name = "Secretaria")] Secretaria,
    [Display(Name = "Financeiro")] Financeiro,
    [Display(Name = "Coordenação")] Coordenacao
}

public static class AreaExt
{
    public const string ClaimAreas = "areas";

    public static IEnumerable<Area> Individuais => Enum.GetValues<Area>().Where(a => a != Area.Nenhuma);
    public static Area Todas => Individuais.Aggregate(Area.Nenhuma, (s, a) => s | a);

    static string Rotulo(Enum e) =>
        e.GetType().GetField(e.ToString())?.GetCustomAttribute<DisplayAttribute>()?.Name ?? e.ToString();

    public static string Nome(this Area a) => Rotulo(a);
    public static string Nome(this Cargo c) => Rotulo(c);

    /// <summary>Áreas típicas de cada cargo.</summary>
    public static Area Padrao(this Cargo c) => c switch
    {
        Cargo.Administrador => Todas,
        Cargo.Secretaria => Area.Matriculas | Area.Alunos | Area.Professores | Area.Disciplinas | Area.Turmas | Area.Desempenho | Area.Calendario | Area.Comunicados,
        Cargo.Financeiro => Area.Financeiro | Area.Calendario,
        _ => Area.Desempenho | Area.Turmas | Area.Disciplinas | Area.Professores | Area.Calendario | Area.Comunicados,
    };

    /// <summary>"Todas" ou os nomes separados por vírgula.</summary>
    public static string Resumo(this Area a)
    {
        if (a == Area.Nenhuma) return "nenhuma";
        if ((a & Todas) == Todas) return "Todas";
        return string.Join(", ", Individuais.Where(x => a.HasFlag(x)).Select(x => x.Nome().Replace(" (consulta)", "").Replace(" (cadastrar eventos)", "").Replace(" (funcionários)", "").Replace(" (mural)", "")));
    }

    /// <summary>Áreas gravadas no cookie de login (só funcionário tem).</summary>
    public static Area Areas(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimAreas), out var n) ? (Area)n : Area.Nenhuma;

    public static bool TemArea(this ClaimsPrincipal user, Area a) => user.IsInRole(nameof(Perfil.Funcionario)) && user.Areas().HasFlag(a);
}

public static class RegrasUsuarios
{
    /// <summary>O sistema nunca pode ficar sem um funcionário ativo com a área Usuários.</summary>
    public static bool RestaAdministrador(IEnumerable<(bool Ativo, Area Areas)> funcionarios) =>
        funcionarios.Any(f => f.Ativo && f.Areas.HasFlag(Area.Usuarios));
}
