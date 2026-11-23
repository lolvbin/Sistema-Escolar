using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Financeiro;

public class IndexModel : PageModel
{
    private readonly EscolaContext _db;
    public IndexModel(EscolaContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public string? Busca { get; set; }
    [BindProperty(SupportsGet = true)] public string? Filtro { get; set; } // atraso | semcobranca | semcontrato | pendente

    public record Linha(int Id, string Aluno, string Responsavel, string Serie, int Ano, StatusMatricula Status,
        bool Contrato, int Cobrancas, decimal EmAberto, int Atrasadas, decimal ValorAtrasado);

    public List<Linha> Itens { get; set; } = new();

    public async Task OnGetAsync()
    {
        var q = _db.Matriculas.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(Busca))
        {
            var b = $"%{Busca.Trim()}%";
            q = q.Where(m => EF.Functions.ILike(m.Aluno!.Nome, b) || EF.Functions.ILike(m.Aluno.Financeiro.Nome, b));
        }
        if (Filtro == "semcontrato") q = q.Where(m => m.ContratoAssinadoEm == null);
        if (Filtro == "pendente") q = q.Where(m => m.Status == StatusMatricula.Pendente);

        var hoje = DateTime.Today;
        var lista = await q.OrderByDescending(m => m.AnoLetivo).ThenBy(m => m.Aluno!.Nome)
            .Select(m => new
            {
                m.Id, Aluno = m.Aluno!.Nome, Resp = m.Aluno.Financeiro.Nome, m.Serie, m.AnoLetivo, m.Status,
                Contrato = m.ContratoAssinadoEm != null,
                Cobrancas = _db.Cobrancas.Count(c => c.MatriculaId == m.Id)
            }).ToListAsync();

        // Valores em aberto com multa e juros de hoje (calculados em C#)
        var ids = lista.Select(x => x.Id).ToList();
        var abertas = (await _db.Cobrancas.AsNoTracking().Where(c => c.PagoEm == null && ids.Contains(c.MatriculaId)).ToListAsync())
            .GroupBy(c => c.MatriculaId).ToDictionary(g => g.Key, g => g.ToList());

        Itens = lista.Select(x =>
        {
            var ab = abertas.GetValueOrDefault(x.Id) ?? new List<Cobranca>();
            var atr = ab.Where(c => c.Atrasada).ToList();
            return new Linha(x.Id, x.Aluno, x.Resp, x.Serie?.Nome() ?? "", x.AnoLetivo, x.Status, x.Contrato, x.Cobrancas,
                ab.Sum(c => c.ValorDevido), atr.Count, atr.Sum(c => c.ValorDevido));
        }).ToList();
        if (Filtro == "atraso") Itens = Itens.Where(i => i.Atrasadas > 0).ToList();
        if (Filtro == "semcobranca") Itens = Itens.Where(i => i.Cobrancas == 0).ToList();
    }
}
