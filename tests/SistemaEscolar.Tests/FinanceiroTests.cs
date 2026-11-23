using SistemaEscolar.Data;

namespace SistemaEscolar.Tests;

public class FinanceiroTests : IDisposable
{
    public FinanceiroTests() => Padrao();
    public void Dispose() => Padrao();
    static void Padrao() { RegrasFinanceiro.MultaPercentual = 2m; RegrasFinanceiro.JurosMensalPercentual = 1m; }

    static Cobranca Cob(DateTime venc, decimal valor = 1000m, TipoCobranca tipo = TipoCobranca.Mensalidade, DateTime? pago = null) =>
        new() { Tipo = tipo, Vencimento = venc, Valor = valor, PagoEm = pago };

    [Theory]
    [InlineData("1.250,50", 1250.50)]
    [InlineData("1250,5", 1250.50)]
    [InlineData("1250.50", 1250.50)]
    [InlineData("R$ 850,00", 850.00)]
    [InlineData("0,999", 1.00)]
    public void Valor_digitado_em_varios_formatos(string txt, double esperado)
    {
        Assert.True(RegrasFinanceiro.LerValor(txt, out var v));
        Assert.Equal((decimal)esperado, v);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-10")]
    [InlineData("100.000,01")]
    public void Valor_invalido(string txt) => Assert.False(RegrasFinanceiro.LerValor(txt, out _));

    [Fact]
    public void Data_no_formato_brasileiro()
    {
        Assert.True(RegrasFinanceiro.LerData("31/01/2027", out var d));
        Assert.Equal(new DateTime(2027, 1, 31), d);
        Assert.False(RegrasFinanceiro.LerData("2027-01-31", out _));
        Assert.False(RegrasFinanceiro.LerData("31/02/2027", out _));
    }

    [Fact]
    public void Sem_atraso_nao_ha_encargos()
    {
        var venc = new DateTime(2026, 12, 10);
        Assert.Equal((0m, 0m), Cob(venc).Encargos(venc));              // no dia
        Assert.Equal((0m, 0m), Cob(venc).Encargos(venc.AddDays(-5)));  // antecipado
    }

    [Fact]
    public void Multa_unica_mais_juros_proporcionais_aos_dias()
    {
        var venc = new DateTime(2026, 10, 24);
        var (multa, juros) = Cob(venc).Encargos(venc.AddDays(30));
        Assert.Equal(20.00m, multa);   // 2% de 1000
        Assert.Equal(10.00m, juros);   // 1% ao mês x 30 dias
        Assert.Equal(1030.00m, Cob(venc).ValorAtualizado(venc.AddDays(30)));
        Assert.Equal(3.33m, Cob(venc).Encargos(venc.AddDays(10)).Juros);
    }

    [Fact]
    public void Taxas_configuraveis()
    {
        RegrasFinanceiro.MultaPercentual = 5m;
        RegrasFinanceiro.JurosMensalPercentual = 3m;
        var venc = new DateTime(2026, 10, 24);
        Assert.Equal((50.00m, 30.00m), Cob(venc).Encargos(venc.AddDays(30)));
    }

    [Fact]
    public void Cobranca_paga_mantem_o_valor_recebido()
    {
        var c = Cob(DateTime.Today.AddDays(-30), pago: DateTime.Today);
        c.ValorPago = 1030m;
        RegrasFinanceiro.MultaPercentual = 10m;   // mudar a taxa depois não altera o que já foi pago
        Assert.Equal(1030m, c.ValorDevido);
        c.ValorPago = null;                        // pagamento antigo sem ValorPago: vale o principal
        Assert.Equal(1000m, c.ValorDevido);
    }

    [Fact]
    public void Cobranca_atrasada_somente_se_aberta_e_vencida()
    {
        Assert.True(Cob(DateTime.Today.AddDays(-1)).Atrasada);
        Assert.False(Cob(DateTime.Today).Atrasada);
        Assert.False(Cob(DateTime.Today.AddDays(-1), pago: DateTime.Today).Atrasada);
    }

    static Matricula Mat(StatusMatricula st = StatusMatricula.Pendente, bool contrato = true) =>
        new() { Status = st, ContratoAssinadoEm = contrato ? DateTime.Today : null };

    [Fact]
    public void Libera_com_contrato_e_taxa_paga()
    {
        var m = Mat();
        var taxa = Cob(DateTime.Today, 300, TipoCobranca.Matricula, DateTime.Today);
        Assert.True(RegrasFinanceiro.TentarLiberar(m, new[] { taxa }));
        Assert.Equal(StatusMatricula.Ativa, m.Status);
    }

    [Fact]
    public void Nao_libera_sem_contrato_sem_taxa_ou_com_taxa_em_aberto()
    {
        var pagaTaxa = Cob(DateTime.Today, 300, TipoCobranca.Matricula, DateTime.Today);
        var abertaTaxa = Cob(DateTime.Today, 300, TipoCobranca.Matricula);
        var mens = Cob(DateTime.Today, 800, TipoCobranca.Mensalidade, DateTime.Today);

        Assert.False(RegrasFinanceiro.TentarLiberar(Mat(contrato: false), new[] { pagaTaxa }));
        Assert.False(RegrasFinanceiro.TentarLiberar(Mat(), new[] { abertaTaxa }));
        Assert.False(RegrasFinanceiro.TentarLiberar(Mat(), new[] { mens }));   // só mensalidade paga não libera
        Assert.False(RegrasFinanceiro.TentarLiberar(Mat(), Array.Empty<Cobranca>()));
    }

    [Theory]
    [InlineData(StatusMatricula.Ativa)]
    [InlineData(StatusMatricula.Cancelada)]
    [InlineData(StatusMatricula.Transferida)]
    public void So_matricula_pendente_e_liberada(StatusMatricula status)
    {
        var m = Mat(status);
        var taxa = Cob(DateTime.Today, 300, TipoCobranca.Matricula, DateTime.Today);
        Assert.False(RegrasFinanceiro.TentarLiberar(m, new[] { taxa }));
        Assert.Equal(status, m.Status);
    }
}
