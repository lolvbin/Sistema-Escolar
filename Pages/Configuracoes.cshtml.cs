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
    public string? ErroMinima { get; set; }
    public string? ErroRecuperacao { get; set; }
    [TempData] public string? Aviso { get; set; }

    public void OnGet()
    {
        Minima = Aprovacao.MediaMinima.ToString("0.0", CultureInfo.CurrentCulture);
        Recuperacao = Aprovacao.MediaRecuperacao.ToString("0.0", CultureInfo.CurrentCulture);
    }

    static bool Ler(string? txt, out decimal v) =>
        decimal.TryParse((txt ?? "").Trim().Replace('.', ','), NumberStyles.Number, CultureInfo.CurrentCulture, out v) && v >= 0 && v <= 10;

    public async Task<IActionResult> OnPostAsync()
    {
        var okMin = Ler(Minima, out var min);
        var okRec = Ler(Recuperacao, out var rec);
        if (!okMin) ErroMinima = "Use um número de 0 a 10";
        if (!okRec) ErroRecuperacao = "Use um número de 0 a 10";
        if (okMin && okRec)
        {
            min = Math.Round(min, 1, MidpointRounding.AwayFromZero);
            rec = Math.Round(rec, 1, MidpointRounding.AwayFromZero);
            if (rec >= min) ErroRecuperacao = "Deve ser menor que a média de aprovação";
        }
        if (ErroMinima != null || ErroRecuperacao != null) return Page();

        var c = await _db.Configuracoes.FirstAsync(x => x.Id == 1);
        c.MediaMinima = min;
        c.MediaRecuperacao = rec;
        await _db.SaveChangesAsync();
        Aprovacao.MediaMinima = min;
        Aprovacao.MediaRecuperacao = rec;

        Aviso = "Configurações salvas. A nova regra vale para todos os boletins.";
        return RedirectToPage();
    }
}
