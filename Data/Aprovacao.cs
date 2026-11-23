using Microsoft.EntityFrameworkCore;

namespace SistemaEscolar.Data;

// Como a nota de recuperação entra na média final (escolha da escola, em Configurações)
public enum ModoRecuperacao
{
    MediaDasDuas = 0,   // (média anual + recuperação) / 2
    MaiorDasDuas = 1,   // vale a maior entre a média anual e a recuperação
    SubstituiMedia = 2  // a recuperação vira a média final, mesmo se menor
}

public enum Situacao { EmAndamento, Aprovado, Recuperacao, Reprovado }

// Regra de aprovação: média anual das 4 notas. Os limites vêm da tabela Configuracoes
// (tela Configurações) e ficam em memória; padrão: >= 6,0 aprovado; de 4,0 a 5,9 recuperação; abaixo de 4,0 reprovado.
// Enquanto faltar nota de algum bimestre a situação é "Em andamento".
public static class Aprovacao
{
    public static decimal MediaMinima { get; set; } = 6.0m;
    public static decimal MediaRecuperacao { get; set; } = 4.0m;
    public static decimal FrequenciaMinima { get; set; } = 75.0m; // % de presença
    public static ModoRecuperacao Modo { get; set; } = ModoRecuperacao.MediaDasDuas;

    public static decimal? Media(IEnumerable<decimal?> notas)
    {
        var l = notas.Where(x => x != null).Select(x => x!.Value).ToList();
        return l.Count == 0 ? null : Math.Round(l.Average(), 1, MidpointRounding.AwayFromZero);
    }

    // Com as 4 notas lançadas: aprovado na média; reprovado abaixo da média de recuperação;
    // entre as duas fica em recuperação até lançarem a nota de recuperação. A média final com
    // recuperação segue o `Modo` escolhido e precisa atingir a média de aprovação.
    public static Situacao DaDisciplina(decimal?[] notas, decimal? rec = null)
    {
        if (notas.Length < TabelaNotas.Bimestres || notas.Any(n => n == null)) return Situacao.EmAndamento;
        var m = Media(notas)!.Value;
        if (m >= MediaMinima) return Situacao.Aprovado;
        if (m < MediaRecuperacao) return Situacao.Reprovado;
        if (rec == null) return Situacao.Recuperacao;
        return MediaFinal(notas, rec)!.Value >= MediaMinima ? Situacao.Aprovado : Situacao.Reprovado;
    }

    // Média que aparece no boletim: a final (com recuperação) quando houver, senão a anual
    public static decimal? MediaFinal(decimal?[] notas, decimal? rec)
    {
        var m = Media(notas);
        if (m == null || rec == null || notas.Any(n => n == null) || m >= MediaMinima || m < MediaRecuperacao) return m;
        return Modo switch
        {
            ModoRecuperacao.MaiorDasDuas => Math.Max(m.Value, rec.Value),
            ModoRecuperacao.SubstituiMedia => rec.Value,
            _ => Math.Round((m.Value + rec.Value) / 2, 1, MidpointRounding.AwayFromZero)
        };
    }

    public static decimal? Frequencia(int faltas, int aulas) =>
        aulas <= 0 ? null : Math.Round(Math.Max(0, 100m - 100m * faltas / aulas), 1, MidpointRounding.AwayFromZero);

    // Geral: com todas as disciplinas definidas, frequência abaixo do mínimo reprova mesmo com boas notas
    public static Situacao Geral(IEnumerable<Situacao> disciplinas, decimal? frequencia = null)
    {
        var l = disciplinas.ToList();
        if (l.Count == 0 || l.Contains(Situacao.EmAndamento)) return Situacao.EmAndamento;
        if (frequencia != null && frequencia < FrequenciaMinima) return Situacao.Reprovado;
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

// Linha única (Id = 1) com os limites de aprovação
public class Configuracao
{
    public int Id { get; set; } = 1;
    public decimal MediaMinima { get; set; } = 6.0m;
    public decimal MediaRecuperacao { get; set; } = 4.0m;
    public decimal FrequenciaMinima { get; set; } = 75.0m;
    public int ModoRecuperacao { get; set; }
    public decimal MultaPercentual { get; set; } = 2.0m;
    public decimal JurosMensal { get; set; } = 1.0m;
}

public static class TabelaConfiguracoes
{
    // A linha padrão (Id = 1) vem da migration. Aqui só carregamos os valores para a memória.
    public static void Carregar(EscolaContext db)
    {
        var c = db.Configuracoes.AsNoTracking().First(x => x.Id == 1);
        Aprovacao.MediaMinima = c.MediaMinima;
        Aprovacao.MediaRecuperacao = c.MediaRecuperacao;
        Aprovacao.FrequenciaMinima = c.FrequenciaMinima;
        Aprovacao.Modo = (ModoRecuperacao)c.ModoRecuperacao;
        RegrasFinanceiro.MultaPercentual = c.MultaPercentual;
        RegrasFinanceiro.JurosMensalPercentual = c.JurosMensal;
    }
}
