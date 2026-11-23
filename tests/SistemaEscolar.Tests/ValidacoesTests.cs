using SistemaEscolar.Data;

namespace SistemaEscolar.Tests;

public class ValidacoesTests
{
    [Theory]
    [InlineData("111.444.777-35", true)]
    [InlineData("11144477735", true)]
    [InlineData("529.982.247-25", true)]
    [InlineData("111.444.777-36", false)]   // dígito verificador errado
    [InlineData("111.111.111-11", false)]   // todos iguais
    [InlineData("123", false)]
    [InlineData("abc.def.ghi-jk", false)]
    public void Cpf_confere_digitos_verificadores(string cpf, bool esperado) =>
        Assert.Equal(esperado, new CpfAttribute().IsValid(cpf));

    [Theory]
    [InlineData("(31)91234-5678", true)]
    [InlineData("31912345678", true)]
    [InlineData("(31)31234-5678", false)]   // não é celular (sem 9)
    [InlineData("(31)9123-4567", false)]    // 10 dígitos
    [InlineData("(31)91234-567a", false)]
    public void Telefone_exige_celular_com_11_digitos(string tel, bool esperado) =>
        Assert.Equal(esperado, new TelefoneAttribute().IsValid(tel));

    [Theory]
    [InlineData("Maria da Silva", true)]
    [InlineData("D'Ávila-Souza", true)]
    [InlineData("José2", false)]
    [InlineData("Ana@", false)]
    public void Nome_aceita_so_letras(string nome, bool esperado) =>
        Assert.Equal(esperado, new NomeAttribute().IsValid(nome));

    [Fact]
    public void Formatadores_aplicam_mascara()
    {
        Assert.Equal("111.444.777-35", Texto.FormatarCpf("11144477735"));
        Assert.Equal("(31)91234-5678", Texto.FormatarTelefone("31912345678"));
    }

    [Theory]
    [InlineData("Abcdefg1", true)]
    [InlineData("curta1", false)]
    [InlineData("somenteletras", false)]
    [InlineData("12345678", false)]
    public void Senha_nova_exige_8_caracteres_com_letra_e_numero(string senha, bool valida) =>
        Assert.Equal(valida, Autenticacao.ValidarSenhaNova(senha) == null);

    [Fact]
    public void Senha_hash_confere_somente_a_senha_certa()
    {
        var hash = Senha.Gerar("Segredo123");
        Assert.True(Senha.Conferir("Segredo123", hash));
        Assert.False(Senha.Conferir("segredo123", hash));
        Assert.NotEqual(hash, Senha.Gerar("Segredo123")); // salt diferente a cada vez
    }

    [Fact]
    public void Senha_provisoria_tem_10_caracteres_legiveis()
    {
        var s = Senha.Provisoria();
        Assert.Equal(10, s.Length);
        Assert.DoesNotContain(s, c => "0O1lI".Contains(c));
    }
}
