using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages;

public class IndexModel : PageModel
{
    private readonly EscolaContext _db;
    public IndexModel(EscolaContext db) => _db = db;

    public Perfil Perfil { get; set; }
    public int Total { get; set; }
    public int Pendentes { get; set; }
    public List<Comunicado> Mural { get; set; } = new();

    public async Task OnGetAsync()
    {
        Perfil = Enum.TryParse<Perfil>(User.FindFirstValue(ClaimTypes.Role), out var p) ? p : Perfil.Aluno;

        Mural = await MuralComunicados.DoUsuarioAsync(_db, User, Perfil);

        // Números reais só para quem pode ver matrículas
        if (User.TemArea(Area.Matriculas))
        {
            Total = await _db.Matriculas.CountAsync();
            Pendentes = await _db.Matriculas.CountAsync(m => m.Status == StatusMatricula.Pendente);
        }
    }
}
