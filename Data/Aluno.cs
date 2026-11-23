using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace SistemaEscolar.Data;

// Cadastro do aluno e da família. Série, turno e ano letivo continuam na Matrícula.
public class Aluno
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Informe o nome do aluno")]
    [StringLength(120)]
    [Nome]
    [Display(Name = "Nome do aluno")]
    public string Nome { get; set; } = "";

    [Required(ErrorMessage = "Informe a data de nascimento (dd/mm/aaaa)")]
    [DataType(DataType.Date)]
    [Display(Name = "Data de nascimento")]
    public DateTime? DataNascimento { get; set; }

    [Required(ErrorMessage = "Informe o CPF do aluno")]
    [StringLength(14)]
    [Cpf]
    [Display(Name = "CPF do aluno")]
    public string Cpf { get; set; } = "";

    [StringLength(500)]
    [Display(Name = "Observações")]
    public string? Observacoes { get; set; }

    [Display(Name = "O responsável financeiro é o mesmo responsável acadêmico")]
    // Login do próprio aluno (criado pelo funcionário); responsáveis são ligados pelo e-mail
    public int? UsuarioId { get; set; }

    public bool MesmoResponsavel { get; set; } = true;

    public Responsavel Academico { get; set; } = new();
    public Responsavel Financeiro { get; set; } = new();

    public void Validar(ModelStateDictionary ms, string prefixo = "")
    {
        var pre = prefixo == "" ? "" : prefixo + ".";
        if (MesmoResponsavel)
        {
            foreach (var k in ms.Keys.Where(k => k.Contains("Financeiro")).ToList())
                ms.Remove(k);
        }

        if (DataNascimento is { } d && (d.Date > DateTime.Today || d.Year < DateTime.Today.Year - 30))
            ms.AddModelError(pre + "DataNascimento", "Data de nascimento inválida");
    }

    // Rematrícula: o aluno já existe, então atualiza os dados dele com o que veio do formulário.
    public void AtualizarDe(Aluno o)
    {
        DataNascimento = o.DataNascimento;
        MesmoResponsavel = o.MesmoResponsavel;
        Academico.Nome = o.Academico.Nome; Academico.Cpf = o.Academico.Cpf;
        Academico.Telefone = o.Academico.Telefone; Academico.Email = o.Academico.Email;
        Financeiro.Nome = o.Financeiro.Nome; Financeiro.Cpf = o.Financeiro.Cpf;
        Financeiro.Telefone = o.Financeiro.Telefone; Financeiro.Email = o.Financeiro.Email;
    }

    public void Normalizar()
    {
        Nome = Nome.Trim();
        Cpf = Texto.FormatarCpf(Cpf);
        Academico.Normalizar();
        if (MesmoResponsavel) Financeiro = Academico.Copia();
        else Financeiro.Normalizar();
    }
}
