namespace SistemaEscolar.Data;

public enum Situacao { EmAndamento, Aprovado, Recuperacao, Reprovado }

// Regra de aprovação (fixa por enquanto): média anual das 4 notas.
// >= 6,0 aprovado; de 4,0 a 5,9 recuperação; abaixo de 4,0 reprovado.
// Enquanto faltar nota de algum bimestre a situação é "Em andamento".
public static class Aprovacao
{
    public const decimal MediaMinima = 6.0m;
    public const decimal MediaRecuperacao = 4.0m;

    public static decimal? Media(IEnumerable<decimal?> notas)
    {
        var l = notas.Where(x => x != null).Select(x => x!.Value).ToList();
        return l.Count == 0 ? null : Math.Round(l.Average(), 1, MidpointRounding.AwayFromZero);
    }

    public static Situacao DaDisciplina(decimal?[] notas)
    {
        if (notas.Length < TabelaNotas.Bimestres || notas.Any(n => n == null)) return Situacao.EmAndamento;
        var m = Media(notas)!.Value;
        return m >= MediaMinima ? Situacao.Aprovado : m >= MediaRecuperacao ? Situacao.Recuperacao : Situacao.Reprovado;
    }

    public static Situacao Geral(IEnumerable<Situacao> disciplinas)
    {
        var l = disciplinas.ToList();
        if (l.Count == 0 || l.Contains(Situacao.EmAndamento)) return Situacao.EmAndamento;
        if (l.Contains(Situacao.Reprovado)) return Situacao.Reprovado;
        return l.Contains(Situacao.Recuperacao) ? Situacao.Recuperacao : Situacao.Aprovado;
    }

    public static string Nome(this Situacao s) => s switch
    {
        Situacao.Aprovado => "Aprovado",
        Situacao.Recuperacao => "Recuperação",
        Situacao.Reprovado => "Reprovado",
        _ => "Em andamento"
    };
}
