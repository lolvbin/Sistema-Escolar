using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages;

public class ConfiguracoesModel : PageModel
{
    private readonly EscolaContext _db;
    public ConfiguracoesModel(EscolaContext db) => _db = db;

    // Texto digitado (aceita vírgula ou ponto)
    [BindProperty] public string? Minima { get; set; }
    [BindProperty] public string? Recuperacao { get; set; }
    [BindProperty] public string? Multa { get; set; }
    [BindProperty] public string? Juros { get; set; }
    public string? ErroMulta { get; set; }
    public string? ErroJuros { get; set; }
    [BindProperty] public int Modo { get; set; }
    [BindProperty] public string? Frequencia { get; set; }
    public string? ErroFrequencia { get; set; }
    public string? ErroMinima { get; set; }
    public string? ErroRecuperacao { get; set; }
    [TempData] public string? Aviso { get; set; }

    public void OnGet()
    {
        Minima = Aprovacao.MediaMinima.ToString("0.0", CultureInfo.CurrentCulture);
        Recuperacao = Aprovacao.MediaRecuperacao.ToString("0.0", CultureInfo.CurrentCulture);
        Frequencia = Aprovacao.FrequenciaMinima.ToString("0.0", CultureInfo.CurrentCulture);
        Modo = (int)Aprovacao.Modo;
        Multa = RegrasFinanceiro.MultaPercentual.ToString("0.0#", CultureInfo.CurrentCulture);
        Juros = RegrasFinanceiro.JurosMensalPercentual.ToString("0.0#", CultureInfo.CurrentCulture);
    }

    static bool Ler(string? txt, out decimal v, decimal max = 10) =>
        decimal.TryParse((txt ?? "").Trim().Replace('.', ','), NumberStyles.Number, CultureInfo.CurrentCulture, out v) && v >= 0 && v <= max;

    public async Task<IActionResult> OnPostAsync()
    {
        var okMin = Ler(Minima, out var min);
        var okRec = Ler(Recuperacao, out var rec);
        if (!Enum.IsDefined(typeof(ModoRecuperacao), Modo)) Modo = 0;
        var okMulta = Ler(Multa, out var multa, 20);
        var okJuros = Ler(Juros, out var juros, 10);
        if (!okMulta) ErroMulta = "Use um número de 0 a 20";
        if (!okJuros) ErroJuros = "Use um número de 0 a 10";
        var okFreq = Ler(Frequencia, out var freq, 100);
        if (!okFreq) ErroFrequencia = "Use um número de 0 a 100";
        if (!okMin) ErroMinima = "Use um número de 0 a 10";
        if (!okRec) ErroRecuperacao = "Use um número de 0 a 10";
        if (okMin && okRec)
        {
            min = Math.Round(min, 1, MidpointRounding.AwayFromZero);
            rec = Math.Round(rec, 1, MidpointRounding.AwayFromZero);
            if (rec >= min) ErroRecuperacao = "Deve ser menor que a média de aprovação";
        }
        if (ErroMinima != null || ErroRecuperacao != null || ErroFrequencia != null || ErroMulta != null || ErroJuros != null) return Page();
        multa = Math.Round(multa, 2, MidpointRounding.AwayFromZero);
        juros = Math.Round(juros, 2, MidpointRounding.AwayFromZero);
        freq = Math.Round(freq, 1, MidpointRounding.AwayFromZero);

        var c = await _db.Configuracoes.FirstAsync(x => x.Id == 1);
        c.MediaMinima = min;
        c.MediaRecuperacao = rec;
        c.FrequenciaMinima = freq;
        c.ModoRecuperacao = Modo;
        c.MultaPercentual = multa;
        c.JurosMensal = juros;
        await _db.SaveChangesAsync();
        Aprovacao.MediaMinima = min;
        Aprovacao.MediaRecuperacao = rec;
        Aprovacao.FrequenciaMinima = freq;
        Aprovacao.Modo = (ModoRecuperacao)Modo;
        RegrasFinanceiro.MultaPercentual = multa;
        RegrasFinanceiro.JurosMensalPercentual = juros;

        Aviso = "Configurações salvas. A nova regra vale para todos os boletins.";
        return RedirectToPage();
    }
}
