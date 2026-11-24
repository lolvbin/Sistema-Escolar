using SistemaEscolar.Data;
using Xunit;

namespace SistemaEscolar.Tests;

public class AuditoriaTests
{
    [Fact] public void Nota_AusenteViraTraco() => Assert.Equal("—", Auditoria.Nota(null));
    [Fact] public void Nota_UsaVirgula() => Assert.Equal("7,5", Auditoria.Nota(7.5m));
    [Fact] public void MudancaNota_MostraAntesEDepois() => Assert.Equal("Ana: 7,0 → 8,5", Auditoria.MudancaNota("Ana", 7m, 8.5m));
    [Fact] public void MudancaNota_NotaNovaEApagada()
    {
        Assert.Equal("Ana: — → 5,0", Auditoria.MudancaNota("Ana", null, 5m));
        Assert.Equal("Ana: 5,0 → —", Auditoria.MudancaNota("Ana", 5m, null));
    }
    [Fact] public void Cortar_RespeitaOLimite()
    {
        Assert.Equal("abc", Auditoria.Cortar("abc", 5));
        var c = Auditoria.Cortar(new string('x', 50), 10);
        Assert.Equal(10, c.Length);
        Assert.EndsWith("…", c);
    }
}
