using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages;

// Visão do responsável financeiro: mensalidades dos alunos ligados ao e-mail dele (somente leitura)
public class MensalidadesModel : PageModel
{
    private readonly EscolaContext _db;
    public MensalidadesModel(EscolaContext db) => _db = db;

    public record Bloco(string Aluno, string Serie, int Ano, DateTime? Contrato, List<Cobranca> Cobrancas);
    public List<Bloco> Blocos { get; set; } = new();

    public async Task OnGetAsync()
    {
        var email = (User.FindFirstValue(ClaimTypes.Email) ?? "").ToLower();
        var ms = await _db.Matriculas.AsNoTracking().Include(m => m.Aluno)
            .Where(m => m.Aluno!.Academico.Email.ToLower() == email || m.Aluno.Financeiro.Email.ToLower() == email)
            .OrderByDescending(m => m.AnoLetivo).ThenBy(m => m.Aluno!.Nome).ToListAsync();
        var ids = ms.Select(m => m.Id).ToList();
        var cobs = await _db.Cobrancas.AsNoTracking().Where(c => ids.Contains(c.MatriculaId)).OrderBy(c => c.Numero).ToListAsync();
        Blocos = ms.Select(m => new Bloco(m.Aluno!.Nome, m.Serie?.Nome() ?? "", m.AnoLetivo, m.ContratoAssinadoEm,
            cobs.Where(c => c.MatriculaId == m.Id).ToList())).ToList();
    }
}
