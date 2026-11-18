using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace SistemaEscolar.Data;

public enum StatusMatricula
{
    [Display(Name = "Pendente")] Pendente,
    [Display(Name = "Ativa")] Ativa,
    [Display(Name = "Cancelada")] Cancelada,
    [Display(Name = "Transferida")] Transferida
}

public enum Turno
{
    [Display(Name = "Manhã")] Manha,
    [Display(Name = "Tarde")] Tarde,
    [Display(Name = "Noite")] Noite
}

// Lista fixa de séries: padroniza nomes para relatórios futuros.
public enum Serie
{
    [Display(Name = "Maternal", GroupName = "Educação Infantil")] Maternal = 1,
    [Display(Name = "Pré I", GroupName = "Educação Infantil")] PreI,
    [Display(Name = "Pré II", GroupName = "Educação Infantil")] PreII,
    [Display(Name = "1º ano", GroupName = "Ensino Fundamental")] Fund1 = 10,
    [Display(Name = "2º ano", GroupName = "Ensino Fundamental")] Fund2,
    [Display(Name = "3º ano", GroupName = "Ensino Fundamental")] Fund3,
    [Display(Name = "4º ano", GroupName = "Ensino Fundamental")] Fund4,
    [Display(Name = "5º ano", GroupName = "Ensino Fundamental")] Fund5,
    [Display(Name = "6º ano", GroupName = "Ensino Fundamental")] Fund6,
    [Display(Name = "7º ano", GroupName = "Ensino Fundamental")] Fund7,
    [Display(Name = "8º ano", GroupName = "Ensino Fundamental")] Fund8,
    [Display(Name = "9º ano", GroupName = "Ensino Fundamental")] Fund9,
    [Display(Name = "1ª série", GroupName = "Ensino Médio")] Medio1 = 20,
    [Display(Name = "2ª série", GroupName = "Ensino Médio")] Medio2,
    [Display(Name = "3ª série", GroupName = "Ensino Médio")] Medio3
}

public class Responsavel
{
    [Required(ErrorMessage = "Informe o nome")]
    [StringLength(120)]
    [Nome]
    [Display(Name = "Nome")]
    public string Nome { get; set; } = "";

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
    [Display(Name = "E-mail")]
    public string Email { get; set; } = "";

    public Responsavel Copia() => new()
    {
        Nome = Nome, Cpf = Cpf, Telefone = Telefone, Email = Email
    };

    public void Normalizar()
    {
        Nome = Nome.Trim();
        Cpf = Texto.FormatarCpf(Cpf);
        Telefone = Texto.FormatarTelefone(Telefone);
        Email = Email.Trim();
    }
}

public class Matricula
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Informe o nome do aluno")]
    [StringLength(120)]
    [Nome]
    [Display(Name = "Nome do aluno")]
    public string AlunoNome { get; set; } = "";

    [Required(ErrorMessage = "Informe a data de nascimento (dd/mm/aaaa)")]
    [DataType(DataType.Date)]
    [Display(Name = "Data de nascimento")]
    public DateTime? DataNascimento { get; set; }

    [Required(ErrorMessage = "Informe o CPF do aluno")]
    [StringLength(14)]
    [Cpf]
    [Display(Name = "CPF do aluno")]
    public string Cpf { get; set; } = "";

    [Required(ErrorMessage = "Selecione a série")]
    [Display(Name = "Série")]
    public Serie? Serie { get; set; }

    [Required(ErrorMessage = "Selecione o turno")]
    public Turno? Turno { get; set; }

    [Display(Name = "Ano letivo")]
    public int AnoLetivo { get; set; } = DateTime.Today.Year + 1;

    public StatusMatricula Status { get; set; } = StatusMatricula.Pendente;

    [DataType(DataType.Date)]
    [Display(Name = "Data da matrícula")]
    public DateTime DataMatricula { get; set; } = DateTime.Today;

    [StringLength(500)]
    [Display(Name = "Observações")]
    public string? Observacoes { get; set; }

    // Responsável acadêmico e financeiro. Se for o mesmo, o financeiro é uma cópia do acadêmico.
    [Display(Name = "O responsável financeiro é o mesmo responsável acadêmico")]
    public bool MesmoResponsavel { get; set; } = true;

    public Responsavel Academico { get; set; } = new();
    public Responsavel Financeiro { get; set; } = new();

    public static int[] AnosPermitidos => new[] { DateTime.Today.Year, DateTime.Today.Year + 1 };

    /// <summary>Regras que dependem de mais de um campo ou da data de hoje.</summary>
    public void Validar(ModelStateDictionary ms)
    {
        if (MesmoResponsavel)
        {
            // Financeiro não vem do formulário: ignora os erros dele.
            foreach (var k in ms.Keys.Where(k => k.Contains("Financeiro")).ToList())
                ms.Remove(k);
        }

        if (DataNascimento is { } d && (d.Date > DateTime.Today || d.Year < DateTime.Today.Year - 30))
            ms.AddModelError("DataNascimento", "Data de nascimento inválida");

        if (!AnosPermitidos.Contains(AnoLetivo))
            ms.AddModelError("AnoLetivo", $"Ano letivo deve ser {AnosPermitidos[0]} ou {AnosPermitidos[1]}");
    }

    public void Normalizar()
    {
        AlunoNome = AlunoNome.Trim();
        Cpf = Texto.FormatarCpf(Cpf);
        Academico.Normalizar();
        if (MesmoResponsavel) Financeiro = Academico.Copia();
        else Financeiro.Normalizar();
    }
}

public class EscolaContext : DbContext
{
    public EscolaContext(DbContextOptions<EscolaContext> options) : base(options) { }

    public DbSet<Matricula> Matriculas => Set<Matricula>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Usuario>().HasIndex(u => u.Email).IsUnique();
        b.Entity<Matricula>().Property(m => m.DataNascimento).HasColumnType("date");
        b.Entity<Matricula>().Property(m => m.DataMatricula).HasColumnType("date");
        b.Entity<Matricula>().OwnsOne(m => m.Academico);
        b.Entity<Matricula>().OwnsOne(m => m.Financeiro);
    }
}
