using Microsoft.EntityFrameworkCore;

namespace SistemaEscolar.Data;

public enum Situacao { EmAndamento, Aprovado, Recuperacao, Reprovado }

// Regra de aprovação: média anual das 4 notas. Os limites vêm da tabela Configuracoes
// (tela Configurações) e ficam em memória; padrão: >= 6,0 aprovado; de 4,0 a 5,9 recuperação; abaixo de 4,0 reprovado.
// Enquanto faltar nota de algum bimestre a situação é "Em andamento".
public static class Aprovacao
{
    public static decimal MediaMinima { get; set; } = 6.0m;
    public static decimal MediaRecuperacao { get; set; } = 4.0m;

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

// Linha única (Id = 1) com os limites de aprovação
public class Configuracao
{
    public int Id { get; set; } = 1;
    public decimal MediaMinima { get; set; } = 6.0m;
    public decimal MediaRecuperacao { get; set; } = 4.0m;
}

public static class TabelaConfiguracoes
{
    public static void Preparar(EscolaContext db)
    {
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "Configuracoes" (
                "Id" integer PRIMARY KEY,
                "MediaMinima" numeric(3,1) NOT NULL,
                "MediaRecuperacao" numeric(3,1) NOT NULL
            );
            INSERT INTO "Configuracoes" ("Id","MediaMinima","MediaRecuperacao") VALUES (1, 6.0, 4.0) ON CONFLICT ("Id") DO NOTHING;
            """);
        var c = db.Configuracoes.AsNoTracking().First(x => x.Id == 1);
        Aprovacao.MediaMinima = c.MediaMinima;
        Aprovacao.MediaRecuperacao = c.MediaRecuperacao;
    }
}
