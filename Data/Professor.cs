using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.EntityFrameworkCore;

namespace SistemaEscolar.Data;

// Cadastro do professor. O login (Usuario, perfil Professor) é criado junto, com o mesmo e-mail.
// Professor não é excluído: fica inativo (e o login é bloqueado).
public class Professor
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Informe o nome do professor")]
    [StringLength(120)]
    [Nome]
    [Display(Name = "Nome")]
    public string Nome { get; set; } = "";

    [Required(ErrorMessage = "Informe a data de nascimento (dd/mm/aaaa)")]
    [DataType(DataType.Date)]
    [Display(Name = "Data de nascimento")]
    public DateTime? DataNascimento { get; set; }

    [Required(ErrorMessage = "Informe o CPF")]
    [StringLength(14)]
    [Cpf]
    [Display(Name = "CPF")]
    public string Cpf { get; set; } = "";

    [Required(ErrorMessage = "Informe o telefone")]
    [StringLength(14)]
    [Telefone]
    [Display(Name = "Telefone")]
    public string Telefone { get; set; } = "";

    [Required(ErrorMessage = "Informe o e-mail")]
    [EmailAddress(ErrorMessage = "E-mail inválido")]
    [StringLength(120)]
    [Display(Name = "E-mail (também é o login)")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Informe a formação")]
    [StringLength(120)]
    [Display(Name = "Formação")]
    public string Formacao { get; set; } = "";

    public bool Ativo { get; set; } = true;

    public int? UsuarioId { get; set; }

    [ValidateNever]
    public Usuario? Usuario { get; set; }

    public void Validar(ModelStateDictionary ms)
    {
        if (DataNascimento is { } d)
        {
            var hoje = DateTime.Today;
            var idade = hoje.Year - d.Year - (d.Date > hoje.AddYears(-(hoje.Year - d.Year)) ? 1 : 0);
            if (d.Date > hoje || idade < 18 || idade > 90)
                ms.AddModelError("DataNascimento", "Data de nascimento inválida (mínimo 18 anos)");
        }
    }

    public void Normalizar()
    {
        Nome = Nome.Trim();
        Cpf = Texto.FormatarCpf(Cpf);
        Telefone = Texto.FormatarTelefone(Telefone);
        Email = Email.Trim().ToLowerInvariant();
        Formacao = Formacao.Trim();
    }
}
