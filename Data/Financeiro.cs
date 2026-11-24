using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace SistemaEscolar.Data;

public enum TipoCobranca
{
    [Display(Name = "Matrícula")] Matricula,
    [Display(Name = "Mensalidade")] Mensalidade
}

public enum FormaPagamento
{
    [Display(Name = "Pix")] Pix,
    [Display(Name = "Dinheiro")] Dinheiro,
    [Display(Name = "Cartão")] Cartao,
    [Display(Name = "Boleto")] Boleto,
    [Display(Name = "Transferência")] Transferencia
}

// Uma cobrança da matrícula (taxa de matrícula ou uma mensalidade). Paga quando PagoEm tem data.
public class Cobranca
{
    public int Id { get; set; }
    public int MatriculaId { get; set; }
    public Matricula? Matricula { get; set; }
    public TipoCobranca Tipo { get; set; }
    public int Numero { get; set; }            // 0 = taxa de matrícula; 1..n = mensalidades
    public string Descricao { get; set; } = "";
    public DateTime Vencimento { get; set; }
    public decimal Valor { get; set; }
    public DateTime? PagoEm { get; set; }
    public FormaPagamento? Forma { get; set; }
    public decimal? ValorPago { get; set; }    // valor recebido (com multa e juros, se houve); fica gravado para o histórico

    [NotMapped] public bool Paga => PagoEm != null;
    [NotMapped] public bool Atrasada => PagoEm == null && Vencimento.Date < DateTime.Today;

    /// <summary>Multa (única, % do valor) e juros (simples, % ao mês proporcional aos dias) de um pagamento feito em <paramref name="ate"/>.</summary>
    public (decimal Multa, decimal Juros) Encargos(DateTime ate)
    {
        var dias = (ate.Date - Vencimento.Date).Days;
        if (dias <= 0) return (0, 0);
        var multa = Math.Round(Valor * RegrasFinanceiro.MultaPercentual / 100m, 2, MidpointRounding.AwayFromZero);
        var juros = Math.Round(Valor * (RegrasFinanceiro.JurosMensalPercentual / 100m) / 30m * dias, 2, MidpointRounding.AwayFromZero);
        return (multa, juros);
    }

    public decimal ValorAtualizado(DateTime ate) { var (m, j) = Encargos(ate); return Valor + m + j; }

    /// <summary>Paga: o que foi recebido. Em aberto: o valor de hoje, com multa e juros se estiver atrasada.</summary>
    [NotMapped] public decimal ValorDevido => PagoEm != null ? ValorPago ?? Valor : ValorAtualizado(DateTime.Today);
}

public static class RegrasFinanceiro
{
    // Carregados da tabela Configuracoes (tela Configurações). Padrão: multa 2% e juros 1% ao mês.
    public static decimal MultaPercentual { get; set; } = 2.0m;
    public static decimal JurosMensalPercentual { get; set; } = 1.0m;

    /// <summary>Lê dinheiro digitado: "1.250,50", "1250,5" ou "1250.50". Positivo, até 100.000,00.</summary>
    public static bool LerValor(string? txt, out decimal valor)
    {
        valor = 0;
        var s = (txt ?? "").Trim().Replace("R$", "").Trim();
        if (s == "") return false;
        if (s.Contains(',')) s = s.Replace(".", "").Replace(',', '.');
        if (!decimal.TryParse(s, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out valor)) return false;
        valor = Math.Round(valor, 2, MidpointRounding.AwayFromZero);
        return valor > 0 && valor <= 100_000;
    }

    public static bool LerData(string? txt, out DateTime data) =>
        DateTime.TryParseExact((txt ?? "").Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out data) && data.Year >= 2000;

    /// <summary>
    /// Libera a matrícula Pendente (vira Ativa) quando o contrato foi assinado e a taxa de matrícula está paga.
    /// Retorna true se mudou o status. Quem chama salva.
    /// </summary>
    public static bool TentarLiberar(Matricula m, IEnumerable<Cobranca> cobrancas)
    {
        if (m.Status != StatusMatricula.Pendente || m.ContratoAssinadoEm == null) return false;
        var taxa = cobrancas.Where(c => c.Tipo == TipoCobranca.Matricula).ToList();
        if (taxa.Count == 0 || taxa.Any(c => !c.Paga)) return false;
        m.Status = StatusMatricula.Ativa;
        return true;
    }
}

// Valor por extenso para o recibo (pt-BR), até 999.999,99.
public static class Extenso
{
    static readonly string[] Centenas = { "", "cento", "duzentos", "trezentos", "quatrocentos", "quinhentos", "seiscentos", "setecentos", "oitocentos", "novecentos" };
    static readonly string[] Ate19 = { "", "um", "dois", "três", "quatro", "cinco", "seis", "sete", "oito", "nove", "dez", "onze", "doze", "treze", "quatorze", "quinze", "dezesseis", "dezessete", "dezoito", "dezenove" };
    static readonly string[] Dezenas = { "", "", "vinte", "trinta", "quarenta", "cinquenta", "sessenta", "setenta", "oitenta", "noventa" };

    static string Ate999(int n)
    {
        if (n == 0) return "";
        if (n == 100) return "cem";
        var partes = new List<string>();
        int c = n / 100, r = n % 100;
        if (c > 0) partes.Add(Centenas[c]);
        if (r > 0)
        {
            if (r < 20) partes.Add(Ate19[r]);
            else { partes.Add(Dezenas[r / 10]); if (r % 10 > 0) partes.Add(Ate19[r % 10]); }
        }
        return string.Join(" e ", partes);
    }

    static string Inteiro(int n)
    {
        if (n < 1000) return Ate999(n);
        int mil = n / 1000, resto = n % 1000;
        var m = mil == 1 ? "mil" : Ate999(mil) + " mil";
        if (resto == 0) return m;
        return m + (resto < 100 || resto % 100 == 0 ? " e " : " ") + Ate999(resto);
    }

    /// <summary>Ex.: 1250,50 → "mil duzentos e cinquenta reais e cinquenta centavos". Vazio se passar de 999.999,99.</summary>
    public static string Reais(decimal valor)
    {
        valor = Math.Round(valor, 2, MidpointRounding.AwayFromZero);
        if (valor < 0 || valor >= 1_000_000) return "";
        int reais = (int)valor, cent = (int)((valor - reais) * 100);
        var partes = new List<string>();
        if (reais > 0) partes.Add(Inteiro(reais) + (reais == 1 ? " real" : " reais"));
        if (cent > 0) partes.Add(Inteiro(cent) + (cent == 1 ? " centavo" : " centavos"));
        return partes.Count == 0 ? "zero real" : string.Join(" e ", partes);
    }
}
