using SistemaEscolar.Data;

namespace SistemaEscolar.Tests;

public class EscolaTests
{
    [Theory]
    [InlineData(null, false)] [InlineData("", false)] [InlineData(" a ", false)]
    [InlineData("Colégio Aurora", true)] [InlineData("  Ab  ", true)]
    public void Nome_valido(string? nome, bool ok) => Assert.Equal(ok, Escola.ValidarNome(nome) == null);

    [Fact] public void Nome_longo_demais_e_recusado() => Assert.NotNull(Escola.ValidarNome(new string('x', 81)));

    [Fact] public void Contato_junta_so_o_que_foi_preenchido()
    {
        var (e, t) = (Escola.Endereco, Escola.Telefone);
        try
        {
            Escola.Endereco = ""; Escola.Telefone = "";
            Assert.Equal("", Escola.Contato);
            Escola.Endereco = "Rua A, 10"; Assert.Equal("Rua A, 10", Escola.Contato);
            Escola.Telefone = "(31) 3333-4444"; Assert.Equal("Rua A, 10 · (31) 3333-4444", Escola.Contato);
            Escola.Endereco = " "; Assert.Equal("(31) 3333-4444", Escola.Contato);
        }
        finally { Escola.Endereco = e; Escola.Telefone = t; }
    }
}
