using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages;

// TEMPORÁRIO: prévia estática (dados fictícios) do que cada perfil vê. Remover quando os módulos existirem.
[AllowAnonymous]
public class PreviaModel : PageModel
{
    public Perfil Perfil { get; set; } = Perfil.Funcionario;

    public void OnGet(string? perfil)
    {
        if (Enum.TryParse<Perfil>(perfil, true, out var p) && Enum.IsDefined(p)) Perfil = p;
    }
}
