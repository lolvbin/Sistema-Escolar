using SistemaEscolar.Data;

namespace SistemaEscolar.Tests;

public class PublicacaoTests
{
    [Theory]
    [InlineData("https://exemplo.com/material.pdf", true)]
    [InlineData("http://exemplo.com", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("ftp://exemplo.com/a", false)]
    [InlineData("exemplo.com", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Link_so_aceita_http_e_https(string? link, bool esperado) =>
        Assert.Equal(esperado, Publicacao.LinkValido(link));

    [Fact]
    public void Link_muito_longo_e_recusado() =>
        Assert.False(Publicacao.LinkValido("https://exemplo.com/" + new string('a', 300)));

    [Fact]
    public void Atividade_com_prazo_vencido_esta_encerrada()
    {
        Assert.True(new Publicacao { Tipo = TipoPublicacao.Atividade, Prazo = DateTime.Today.AddDays(-1) }.Encerrada);
        Assert.False(new Publicacao { Tipo = TipoPublicacao.Atividade, Prazo = DateTime.Today }.Encerrada);   // vale até o fim do dia
        Assert.False(new Publicacao { Tipo = TipoPublicacao.Aviso, Prazo = DateTime.Today.AddDays(-5) }.Encerrada);
    }
}
