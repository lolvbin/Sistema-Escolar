using SistemaEscolar.Data;

namespace SistemaEscolar.Tests;

// Mexe nas configurações estáticas: sempre volta ao padrão no fim.
public class AprovacaoTests : IDisposable
{
    public AprovacaoTests() => Padrao();
    public void Dispose() => Padrao();

    static void Padrao()
    {
        Aprovacao.MediaMinima = 6.0m;
        Aprovacao.MediaRecuperacao = 4.0m;
        Aprovacao.FrequenciaMinima = 75.0m;
        Aprovacao.Modo = ModoRecuperacao.MediaDasDuas;
    }

    static decimal?[] Notas(params decimal?[] n) => n;

    [Fact]
    public void Media_ignora_notas_nao_lancadas_e_arredonda_para_cima()
    {
        Assert.Null(Aprovacao.Media(Notas(null, null)));
        Assert.Equal(6.5m, Aprovacao.Media(Notas(6, 7, null, null)));
        Assert.Equal(4.8m, Aprovacao.Media(Notas(5, 5, 4, 5))); // 4,75
    }

    [Theory]
    [InlineData(6, Situacao.Aprovado)]
    [InlineData(7, Situacao.Aprovado)]
    [InlineData(5.9, Situacao.Recuperacao)]
    [InlineData(4, Situacao.Recuperacao)]
    [InlineData(3.9, Situacao.Reprovado)]
    public void Situacao_pela_media_anual(double media, Situacao esperado)
    {
        var m = (decimal)media;
        Assert.Equal(esperado, Aprovacao.DaDisciplina(Notas(m, m, m, m)));
    }

    [Fact]
    public void Sem_as_4_notas_fica_em_andamento() =>
        Assert.Equal(Situacao.EmAndamento, Aprovacao.DaDisciplina(Notas(10, 10, null, null)));

    [Theory]
    [InlineData(ModoRecuperacao.MediaDasDuas, 7, 6.0, Situacao.Aprovado)]     // (5 + 7) / 2
    [InlineData(ModoRecuperacao.MediaDasDuas, 6, 5.5, Situacao.Reprovado)]
    [InlineData(ModoRecuperacao.MaiorDasDuas, 7, 7.0, Situacao.Aprovado)]
    [InlineData(ModoRecuperacao.MaiorDasDuas, 4, 5.0, Situacao.Reprovado)]    // fica a média anual
    [InlineData(ModoRecuperacao.SubstituiMedia, 9, 9.0, Situacao.Aprovado)]
    [InlineData(ModoRecuperacao.SubstituiMedia, 4, 4.0, Situacao.Reprovado)]
    public void Recuperacao_conforme_o_modelo_da_escola(ModoRecuperacao modo, double rec, double final, Situacao esperado)
    {
        Aprovacao.Modo = modo;
        var notas = Notas(5, 5, 5, 5);
        Assert.Equal((decimal)final, Aprovacao.MediaFinal(notas, (decimal)rec));
        Assert.Equal(esperado, Aprovacao.DaDisciplina(notas, (decimal)rec));
    }

    [Fact]
    public void Recuperacao_so_vale_para_quem_esta_na_faixa()
    {
        // média 8 (aprovado) ou 3 (reprovado): a nota de recuperação não muda nada
        Assert.Equal(8m, Aprovacao.MediaFinal(Notas(8, 8, 8, 8), 2));
        Assert.Equal(3m, Aprovacao.MediaFinal(Notas(3, 3, 3, 3), 10));
        Assert.Equal(Situacao.Reprovado, Aprovacao.DaDisciplina(Notas(3, 3, 3, 3), 10));
    }

    [Fact]
    public void Limites_configuraveis_mudam_o_resultado()
    {
        Aprovacao.MediaMinima = 7m;
        Aprovacao.MediaRecuperacao = 5m;
        Assert.Equal(Situacao.Recuperacao, Aprovacao.DaDisciplina(Notas(6, 6, 6, 6)));
        Assert.Equal(Situacao.Reprovado, Aprovacao.DaDisciplina(Notas(4, 4, 4, 4)));
    }

    [Theory]
    [InlineData(0, 40, 100.0)]
    [InlineData(10, 40, 75.0)]
    [InlineData(15, 40, 62.5)]
    [InlineData(50, 40, 0.0)]   // nunca negativa
    public void Frequencia_em_percentual(int faltas, int aulas, double esperado) =>
        Assert.Equal((decimal)esperado, Aprovacao.Frequencia(faltas, aulas));

    [Fact]
    public void Frequencia_sem_aulas_dadas_e_nula() => Assert.Null(Aprovacao.Frequencia(0, 0));

    [Fact]
    public void Situacao_geral()
    {
        Assert.Equal(Situacao.EmAndamento, Aprovacao.Geral(new[] { Situacao.Aprovado, Situacao.EmAndamento }));
        Assert.Equal(Situacao.EmAndamento, Aprovacao.Geral(Array.Empty<Situacao>()));
        Assert.Equal(Situacao.Aprovado, Aprovacao.Geral(new[] { Situacao.Aprovado, Situacao.Aprovado }));
        Assert.Equal(Situacao.Recuperacao, Aprovacao.Geral(new[] { Situacao.Aprovado, Situacao.Recuperacao }));
        Assert.Equal(Situacao.Reprovado, Aprovacao.Geral(new[] { Situacao.Recuperacao, Situacao.Reprovado }));
    }

    [Fact]
    public void Frequencia_baixa_reprova_so_com_todas_as_disciplinas_definidas()
    {
        var aprovadas = new[] { Situacao.Aprovado, Situacao.Aprovado };
        Assert.Equal(Situacao.Reprovado, Aprovacao.Geral(aprovadas, 70m));
        Assert.Equal(Situacao.Aprovado, Aprovacao.Geral(aprovadas, 75m));
        Assert.Equal(Situacao.EmAndamento, Aprovacao.Geral(new[] { Situacao.Aprovado, Situacao.EmAndamento }, 10m));
    }
}
