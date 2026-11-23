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
                Cobrancas = _db.Cobrancas.Count(c => c.MatriculaId == m.Id),
                EmAberto = _db.Cobrancas.Where(c => c.MatriculaId == m.Id && c.PagoEm == null).Sum(c => (decimal?)c.Valor) ?? 0,
                Atrasadas = _db.Cobrancas.Count(c => c.MatriculaId == m.Id && c.PagoEm == null && c.Vencimento < hoje),
                ValorAtrasado = _db.Cobrancas.Where(c => c.MatriculaId == m.Id && c.PagoEm == null && c.Vencimento < hoje).Sum(c => (decimal?)c.Valor) ?? 0
            }).ToListAsync();

        Itens = lista.Select(x => new Linha(x.Id, x.Aluno, x.Resp, x.Serie?.Nome() ?? "", x.AnoLetivo, x.Status,
            x.Contrato, x.Cobrancas, x.EmAberto, x.Atrasadas, x.ValorAtrasado)).ToList();
        if (Filtro == "atraso") Itens = Itens.Where(i => i.Atrasadas > 0).ToList();
        if (Filtro == "semcobranca") Itens = Itens.Where(i => i.Cobrancas == 0).ToList();
    }
}
