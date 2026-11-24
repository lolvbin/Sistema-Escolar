using System.Text;
using SistemaEscolar.Data;

namespace SistemaEscolar.Tests;

public class LogoTests
{
    static byte[] Txt(string s) => Encoding.UTF8.GetBytes(s);
    static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0 };

    [Fact] public void Png_e_aceito() => Assert.Equal("image/png", Logos.Validar(Png, out _));
    [Fact] public void Jpeg_e_aceito() => Assert.Equal("image/jpeg", Logos.Validar(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0 }, out _));
    [Fact] public void Svg_simples_e_aceito() => Assert.Equal("image/svg+xml", Logos.Validar(Txt("<?xml version=\"1.0\"?><svg xmlns=\"http://www.w3.org/2000/svg\"><circle r=\"5\"/></svg>"), out _));

    [Theory]
    [InlineData("<svg><script>alert(1)</script></svg>")]
    [InlineData("<svg onload=\"alert(1)\"></svg>")]
    [InlineData("<svg><a href=\"javascript:alert(1)\"/></svg>")]
    [InlineData("<svg><foreignObject/></svg>")]
    public void Svg_com_conteudo_ativo_e_recusado(string svg)
    {
        Assert.Null(Logos.Validar(Txt(svg), out var erro));
        Assert.NotNull(erro);
    }

    [Fact] public void Texto_qualquer_e_recusado() { Assert.Null(Logos.Validar(Txt("oi, sou um txt"), out var e)); Assert.NotNull(e); }
    [Fact] public void Vazio_e_recusado() => Assert.Null(Logos.Validar(Array.Empty<byte>(), out _));
    [Fact] public void Grande_demais_e_recusado()
    {
        var b = new byte[Logos.MaxBytes + 1]; Array.Copy(Png, b, Png.Length);
        Assert.Null(Logos.Validar(b, out var e)); Assert.Contains("512", e);
    }
}
