using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace SistemaEscolar.Data;

/// <summary>Uma linha do registro de ações sensíveis: quem fez, quando, o quê.</summary>
public class RegistroLog
{
    public int Id { get; set; }
    public DateTime Em { get; set; }              // hora local do servidor (timestamp sem fuso)
    public int? UsuarioId { get; set; }           // sem FK: o registro sobrevive ao usuário
    [MaxLength(120)] public string UsuarioNome { get; set; } = "";
    [MaxLength(40)] public string Acao { get; set; } = "";
    [MaxLength(600)] public string Detalhe { get; set; } = "";
}

public static class Auditoria
{
    public const string Pagamento = "Pagamento";
    public const string Cobrancas = "Cobranças";
    public const string Contrato = "Contrato";
    public const string Matricula = "Matrícula";
    public const string Notas = "Notas";
    public const string Frequencia = "Frequência";
    public const string Configuracoes = "Configurações";
    public const string Usuarios = "Usuários";
    public const string Acesso = "Acesso";
    public const string Comunicados = "Comunicados";
    public const string Horarios = "Horários";

    public static readonly string[] Acoes =
        { Pagamento, Cobrancas, Contrato, Matricula, Notas, Frequencia, Configuracoes, Usuarios, Acesso, Comunicados, Horarios };

    /// <summary>Adiciona o registro ao contexto. Quem chama grava no mesmo SaveChanges da ação (ou chama SaveChanges depois).</summary>
    public static void Registrar(this EscolaContext db, ClaimsPrincipal? user, string acao, string detalhe)
    {
        int? id = int.TryParse(user?.FindFirstValue(ClaimTypes.NameIdentifier), out var i) ? i : null;
        db.Registros.Add(new RegistroLog
        {
            Em = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified),
            UsuarioId = id,
            UsuarioNome = Cortar(user?.FindFirstValue(ClaimTypes.Name) ?? "(sistema)", 120),
            Acao = acao,
            Detalhe = Cortar(detalhe, 600),
        });
    }

    public static string Cortar(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

    /// <summary>"7,5" ou "—" para nota ausente.</summary>
    public static string Nota(decimal? v) => v == null ? "—" : v.Value.ToString("0.0", new System.Globalization.CultureInfo("pt-BR"));

    /// <summary>"Maria: 7,0 → 8,5". Só faz sentido se antes != depois.</summary>
    public static string MudancaNota(string aluno, decimal? antes, decimal? depois) =>
        $"{aluno}: {Nota(antes)} → {Nota(depois)}";
}
