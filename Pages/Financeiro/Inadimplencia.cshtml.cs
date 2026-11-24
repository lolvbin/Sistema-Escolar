using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Financeiro;

public class InadimplenciaModel : PageModel
{
    private readonly EscolaContext _db;
    public InadimplenciaModel(EscolaContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public int? Ano { get; set; }
    [BindProperty(SupportsGet = true)] public string? Faixa { get; set; } // ate30 | 31a60 | mais60

    public record Linha(int MatriculaId, string Aluno, string Serie, int Ano, string Responsavel, string Telefone, string Email,
        int Parcelas, DateTime MaisAntiga, int DiasAtraso, decimal Principal, decimal Encargos)
    {
        public decimal Total => Principal + Encargos;
    }

    public List<Linha> Itens { get; set; } = new();
    public decimal TotalGeral { get; set; }
    public decimal TotalPrincipal { get; set; }
    public int Faixa30 { get; set; }
    public int Faixa60 { get; set; }
    public int FaixaMais { get; set; }
    public int MatriculasComCobranca { get; set; }

    public async Task OnGetAsync()
    {
        var hoje = DateTime.Today;
        var baseQ = _db.Cobrancas.AsNoTracking().AsQueryable();
        if (Ano != null) baseQ = baseQ.Where(c => c.Matricula!.AnoLetivo == Ano);

        MatriculasComCobranca = await baseQ.Select(c => c.MatriculaId).Distinct().CountAsync();

        var atrasadas = await baseQ.Where(c => c.PagoEm == null && c.Vencimento < hoje)
            .Include(c => c.Matricula).ThenInclude(m => m!.Aluno).ToListAsync();

        var todas = atrasadas.GroupBy(c => c.MatriculaId).Select(g =>
        {
            var m = g.First().Matricula!; var a = m.Aluno!;
            var antiga = g.Min(c => c.Vencimento).Date;
            var principal = g.Sum(c => c.Valor);
            var total = g.Sum(c => c.ValorAtualizado(hoje));
            return new Linha(m.Id, a.Nome, m.Serie?.Nome() ?? "", m.AnoLetivo, a.Financeiro.Nome, a.Financeiro.Telefone, a.Financeiro.Email,
                g.Count(), antiga, (hoje - antiga).Days, principal, total - principal);
        }).ToList();

        // Resumo (sempre sobre todos os inadimplentes do ano, antes do filtro de faixa)
        TotalGeral = todas.Sum(l => l.Total);
        TotalPrincipal = todas.Sum(l => l.Principal);
        Faixa30 = todas.Count(l => l.DiasAtraso <= 30);
        Faixa60 = todas.Count(l => l.DiasAtraso is > 30 and <= 60);
        FaixaMais = todas.Count(l => l.DiasAtraso > 60);

        Itens = (Faixa switch
        {
            "ate30" => todas.Where(l => l.DiasAtraso <= 30),
            "31a60" => todas.Where(l => l.DiasAtraso is > 30 and <= 60),
            "mais60" => todas.Where(l => l.DiasAtraso > 60),
            _ => todas
        }).OrderByDescending(l => l.Total).ToList();
    }
}
