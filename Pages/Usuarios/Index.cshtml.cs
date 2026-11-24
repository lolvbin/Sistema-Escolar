using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Usuarios;

public class IndexModel : PageModel
{
    private readonly EscolaContext _db;
    public IndexModel(EscolaContext db) => _db = db;

    public List<Usuario> Itens { get; set; } = new();
    public int MeuId { get; set; }

    public async Task OnGetAsync()
    {
        MeuId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        Itens = await _db.Usuarios.AsNoTracking().Where(u => u.Perfil == Perfil.Funcionario).OrderBy(u => u.Nome).ToListAsync();
    }
}
