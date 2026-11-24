using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Usuarios;

// Dados do formulário de funcionário (criar e editar). Validação feita no servidor.
public class UsuarioForm
{
    public string? Nome { get; set; }
    public string? Email { get; set; }
    public Cargo? Cargo { get; set; }
    public List<int> Marcadas { get; set; } = new();   // valores das áreas marcadas
    public bool Ativo { get; set; } = true;

    public Area Areas => Marcadas.Aggregate(Area.Nenhuma, (s, m) => s | ((Area)m & AreaExt.Todas));

    public void Normalizar()
    {
        Nome = (Nome ?? "").Trim();
        Email = (Email ?? "").Trim().ToLowerInvariant();
    }

    public void Validar(ModelStateDictionary ms)
    {
        Normalizar();
        if (Nome == "") ms.AddModelError("Input.Nome", "Informe o nome");
        else if (Nome!.Length > 120 || !new NomeAttribute().IsValid(Nome)) ms.AddModelError("Input.Nome", "Use apenas letras (até 120 caracteres)");
        if (Email == "") ms.AddModelError("Input.Email", "Informe o e-mail (é o login)");
        else if (Email!.Length > 120 || !new EmailAddressAttribute().IsValid(Email)) ms.AddModelError("Input.Email", "E-mail inválido");
        if (Cargo == null) ms.AddModelError("Input.Cargo", "Selecione o cargo");
        if (Areas == Area.Nenhuma) ms.AddModelError("Input.Marcadas", "Marque pelo menos uma área");
    }
}
