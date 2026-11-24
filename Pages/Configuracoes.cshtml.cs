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
    [BindProperty] public string? NomeEscola { get; set; }
    [BindProperty] public string? Endereco { get; set; }
    [BindProperty] public string? Telefone { get; set; }
    public string? ErroNome { get; set; }
    public string? ErroEndereco { get; set; }
    public string? ErroTelefone { get; set; }
    [TempData] public string? Aviso { get; set; }

    public void OnGet()
    {
        NomeEscola = Escola.Nome; Endereco = Escola.Endereco; Telefone = Escola.Telefone;
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
        ErroNome = Escola.ValidarNome(NomeEscola);
        if ((Endereco ?? "").Trim().Length > 150) ErroEndereco = "Use no máximo 150 caracteres";
        if ((Telefone ?? "").Trim().Length > 30) ErroTelefone = "Use no máximo 30 caracteres";
        if (ErroNome != null || ErroEndereco != null || ErroTelefone != null || ErroMinima != null || ErroRecuperacao != null || ErroFrequencia != null || ErroMulta != null || ErroJuros != null) return Page();
        multa = Math.Round(multa, 2, MidpointRounding.AwayFromZero);
        juros = Math.Round(juros, 2, MidpointRounding.AwayFromZero);
        freq = Math.Round(freq, 1, MidpointRounding.AwayFromZero);

        var c = await _db.Configuracoes.FirstAsync(x => x.Id == 1);
        var cult = CultureInfo.CurrentCulture;
        var mud = new List<string>();
        void Dif(string nome, object antes, object depois) { if (!Equals(antes, depois)) mud.Add($"{nome}: {antes} → {depois}"); }
        Dif("média aprovação", c.MediaMinima.ToString("0.0", cult), min.ToString("0.0", cult));
        Dif("média recuperação", c.MediaRecuperacao.ToString("0.0", cult), rec.ToString("0.0", cult));
        Dif("frequência mínima", c.FrequenciaMinima.ToString("0.0", cult), freq.ToString("0.0", cult));
        Dif("modelo de recuperação", c.ModoRecuperacao, Modo);
        Dif("multa %", c.MultaPercentual.ToString("0.##", cult), multa.ToString("0.##", cult));
        Dif("juros %/mês", c.JurosMensal.ToString("0.##", cult), juros.ToString("0.##", cult));
        var nome = NomeEscola!.Trim(); var end = (Endereco ?? "").Trim(); var tel = (Telefone ?? "").Trim();
        Dif("nome da escola", c.NomeEscola, nome);
        Dif("endereço", c.Endereco, end);
        Dif("telefone", c.Telefone, tel);
        c.NomeEscola = nome; c.Endereco = end; c.Telefone = tel;
        if (mud.Count > 0) _db.Registrar(User, Auditoria.Configuracoes, string.Join("; ", mud));
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
        Escola.Nome = nome; Escola.Endereco = end; Escola.Telefone = tel;

        Aviso = "Configurações salvas. A nova regra vale para todos os boletins.";
        return RedirectToPage();
    }
}
