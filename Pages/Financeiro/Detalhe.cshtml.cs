using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Financeiro;

public class DetalheModel : PageModel
{
    private readonly EscolaContext _db;
    public DetalheModel(EscolaContext db) => _db = db;

    public Matricula Matricula { get; set; } = null!;
    public List<Cobranca> Cobrancas { get; set; } = new();

    // Formulário "Gerar cobranças" (texto digitado; validado no servidor)
    [BindProperty] public string? Taxa { get; set; }
    [BindProperty] public string? VencTaxa { get; set; }
    [BindProperty] public string? Mensalidade { get; set; }
    [BindProperty] public int Parcelas { get; set; } = 12;
    [BindProperty] public string? VencPrimeira { get; set; }
    public Dictionary<string, string> Erros { get; set; } = new();

    [TempData] public string? Aviso { get; set; }
    [TempData] public string? Erro { get; set; }

    private async Task<bool> Carregar(int id)
    {
        var m = await _db.Matriculas.Include(x => x.Aluno).FirstOrDefaultAsync(x => x.Id == id);
        if (m == null) return false;
        Matricula = m;
        Cobrancas = await _db.Cobrancas.AsNoTracking().Where(c => c.MatriculaId == id)
            .OrderBy(c => c.Numero).ToListAsync();
        return true;
    }

    public async Task<IActionResult> OnGetAsync(int id) => await Carregar(id) ? Page() : NotFound();

    // Cria a taxa de matrícula (opcional) e as mensalidades. Só quando ainda não há cobranças.
    public async Task<IActionResult> OnPostGerarAsync(int id)
    {
        if (!await Carregar(id)) return NotFound();
        if (Cobrancas.Count > 0) { Erro = "Esta matrícula já tem cobranças. Apague as em aberto para gerar de novo."; return RedirectToPage(new { id }); }

        decimal taxa = 0, mens = 0; DateTime vTaxa = default, vPrim = default;
        if (!RegrasFinanceiro.LerValor(Mensalidade, out mens)) Erros["Mensalidade"] = "Informe o valor da mensalidade (ex.: 850,00)";
        if (Parcelas < 1 || Parcelas > 12) Erros["Parcelas"] = "Use de 1 a 12 parcelas";
        if (!RegrasFinanceiro.LerData(VencPrimeira, out vPrim)) Erros["VencPrimeira"] = "Informe a data (dd/mm/aaaa)";
        if (!string.IsNullOrWhiteSpace(Taxa))
        {
            if (!RegrasFinanceiro.LerValor(Taxa, out taxa)) Erros["Taxa"] = "Valor inválido (ex.: 300,00) ou deixe em branco";
            if (!RegrasFinanceiro.LerData(VencTaxa, out vTaxa)) Erros["VencTaxa"] = "Informe o vencimento da taxa (dd/mm/aaaa)";
        }
        if (Erros.Count > 0) return Page();

        var lista = new List<Cobranca>();
        if (taxa > 0)
            lista.Add(new Cobranca { MatriculaId = id, Tipo = TipoCobranca.Matricula, Numero = 0, Descricao = "Taxa de matrícula", Vencimento = vTaxa, Valor = taxa });
        for (int i = 1; i <= Parcelas; i++)
        {
            var v = vPrim.AddMonths(i - 1);
            lista.Add(new Cobranca { MatriculaId = id, Tipo = TipoCobranca.Mensalidade, Numero = i, Descricao = $"Mensalidade {i:00}/{Parcelas:00} ({v:MM/yyyy})", Vencimento = v, Valor = mens });
        }
        _db.Cobrancas.AddRange(lista);
        await _db.SaveChangesAsync();
        Aviso = $"{lista.Count} cobranças geradas.";
        return RedirectToPage(new { id });
    }

    // Apaga só as cobranças ainda não pagas (para refazer o plano)
    public async Task<IActionResult> OnPostApagarAbertasAsync(int id)
    {
        var abertas = await _db.Cobrancas.Where(c => c.MatriculaId == id && c.PagoEm == null).ToListAsync();
        _db.Cobrancas.RemoveRange(abertas);
        await _db.SaveChangesAsync();
        Aviso = $"{abertas.Count} cobranças em aberto apagadas.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostBaixaAsync(int id, int cobId, string? pagoEm, FormaPagamento? forma)
    {
        var c = await _db.Cobrancas.FirstOrDefaultAsync(x => x.Id == cobId && x.MatriculaId == id);
        if (c == null) return NotFound();
        if (c.PagoEm != null) { Erro = "Essa cobrança já está paga."; return RedirectToPage(new { id }); }
        if (!RegrasFinanceiro.LerData(pagoEm, out var data) || data.Date > DateTime.Today)
        { Erro = "Data do pagamento inválida (dd/mm/aaaa, não pode ser no futuro)."; return RedirectToPage(new { id }); }
        if (forma == null) { Erro = "Selecione a forma de pagamento."; return RedirectToPage(new { id }); }

        c.PagoEm = data.Date;
        c.Forma = forma;
        await _db.SaveChangesAsync();
        Aviso = $"Pagamento registrado: {c.Descricao}.";
        await Liberar(id);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostEstornarAsync(int id, int cobId)
    {
        var c = await _db.Cobrancas.FirstOrDefaultAsync(x => x.Id == cobId && x.MatriculaId == id);
        if (c == null) return NotFound();
        c.PagoEm = null;
        c.Forma = null;
        await _db.SaveChangesAsync();
        Aviso = "Pagamento desfeito. (Se a matrícula já tinha sido liberada, o status não volta sozinho.)";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostContratoAsync(int id, string? assinadoEm)
    {
        var m = await _db.Matriculas.FindAsync(id);
        if (m == null) return NotFound();
        if (string.IsNullOrWhiteSpace(assinadoEm)) { m.ContratoAssinadoEm = null; Aviso = "Assinatura do contrato removida."; }
        else if (RegrasFinanceiro.LerData(assinadoEm, out var d) && d.Date <= DateTime.Today) { m.ContratoAssinadoEm = d.Date; Aviso = "Contrato registrado como assinado."; }
        else { Erro = "Data do contrato inválida (dd/mm/aaaa, não pode ser no futuro)."; return RedirectToPage(new { id }); }
        await _db.SaveChangesAsync();
        await Liberar(id);
        return RedirectToPage(new { id });
    }

    // Liberação manual pelo funcionário (sem esperar pagamento/contrato)
    public async Task<IActionResult> OnPostLiberarAsync(int id)
    {
        var m = await _db.Matriculas.FindAsync(id);
        if (m == null) return NotFound();
        if (m.Status == StatusMatricula.Pendente) { m.Status = StatusMatricula.Ativa; await _db.SaveChangesAsync(); Aviso = "Matrícula liberada manualmente (Ativa)."; }
        return RedirectToPage(new { id });
    }

    // Se contrato assinado e taxa paga, a matrícula Pendente vira Ativa sozinha
    private async Task Liberar(int id)
    {
        var m = await _db.Matriculas.FindAsync(id);
        var cobs = await _db.Cobrancas.AsNoTracking().Where(c => c.MatriculaId == id).ToListAsync();
        if (m != null && RegrasFinanceiro.TentarLiberar(m, cobs))
        {
            await _db.SaveChangesAsync();
            Aviso += " Contrato assinado e taxa paga: matrícula liberada (Ativa).";
        }
    }
}
