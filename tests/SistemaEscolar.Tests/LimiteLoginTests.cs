using SistemaEscolar.Data;

namespace SistemaEscolar.Tests;

public class LimiteLoginTests
{
    [Fact]
    public void Bloqueia_depois_do_maximo_de_falhas()
    {
        var k = "teste:" + Guid.NewGuid();
        for (int i = 0; i < LimiteLogin.MaxPorEmail - 1; i++)
        {
            LimiteLogin.Falhou(k);
            Assert.Null(LimiteLogin.Bloqueado(k, LimiteLogin.MaxPorEmail));
        }
        LimiteLogin.Falhou(k);
        var espera = LimiteLogin.Bloqueado(k, LimiteLogin.MaxPorEmail);
        Assert.NotNull(espera);
        Assert.InRange(espera!.Value.TotalMinutes, 14, 15.01);
    }

    [Fact]
    public void Login_certo_zera_o_contador()
    {
        var k = "teste:" + Guid.NewGuid();
        for (int i = 0; i < LimiteLogin.MaxPorEmail; i++) LimiteLogin.Falhou(k);
        LimiteLogin.Limpar(k);
        Assert.Null(LimiteLogin.Bloqueado(k, LimiteLogin.MaxPorEmail));
    }

    [Fact]
    public void Chaves_diferentes_nao_se_afetam()
    {
        var a = "teste:" + Guid.NewGuid();
        var b = "teste:" + Guid.NewGuid();
        for (int i = 0; i < LimiteLogin.MaxPorEmail; i++) LimiteLogin.Falhou(a);
        Assert.Null(LimiteLogin.Bloqueado(b, LimiteLogin.MaxPorEmail));
    }

    [Theory]
    [InlineData(0.2, "1 minuto")]
    [InlineData(1, "1 minuto")]
    [InlineData(14.1, "15 minutos")]
    public void Texto_do_tempo_restante(double minutos, string esperado) =>
        Assert.Equal(esperado, LimiteLogin.Minutos(TimeSpan.FromMinutes(minutos)));
}
