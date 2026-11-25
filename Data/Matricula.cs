using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
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

    // Definido pelo sistema: o aluno é criado (ou reaproveitado) junto com a matrícula.
    public int? AlunoId { get; set; }

    [ValidateNever]
    public Aluno? Aluno { get; set; }

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

    // Data em que o contrato foi assinado (informada pelo funcionário na tela Financeiro)
    public DateTime? ContratoAssinadoEm { get; set; }

    // Turma em que o aluno está enturmado (definida pela tela de Turmas, nunca pelo formulário)
    public int? TurmaId { get; set; }

    [ValidateNever]
    public Turma? Turma { get; set; }

    public static int[] AnosPermitidos => new[] { DateTime.Today.Year, DateTime.Today.Year + 1 };

    /// <summary>Regras que dependem de mais de um campo ou da data de hoje.</summary>
    public void Validar(ModelStateDictionary ms, string prefixo = "")
    {
        var pre = prefixo == "" ? "" : prefixo + ".";
        if (!AnosPermitidos.Contains(AnoLetivo))
            ms.AddModelError(pre + "AnoLetivo", $"Ano letivo deve ser {AnosPermitidos[0]} ou {AnosPermitidos[1]}");
    }
}

public class EscolaContext : DbContext
{
    public EscolaContext(DbContextOptions<EscolaContext> options) : base(options) { }

    public DbSet<Matricula> Matriculas => Set<Matricula>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Aluno> Alunos => Set<Aluno>();
    public DbSet<Professor> Professores => Set<Professor>();
    public DbSet<Disciplina> Disciplinas => Set<Disciplina>();
    public DbSet<Turma> Turmas => Set<Turma>();
    public DbSet<Nota> Notas => Set<Nota>();
    public DbSet<Evento> Eventos => Set<Evento>();
    public DbSet<Frequencia> Frequencias => Set<Frequencia>();
    public DbSet<AulasDadas> Aulas => Set<AulasDadas>();
    public DbSet<Cobranca> Cobrancas => Set<Cobranca>();
    public DbSet<Configuracao> Configuracoes => Set<Configuracao>();
    public DbSet<Publicacao> Publicacoes => Set<Publicacao>();
    public DbSet<RegistroLog> Registros => Set<RegistroLog>();
    public DbSet<LogoEscola> Logos => Set<LogoEscola>();
    public DbSet<Comunicado> Comunicados => Set<Comunicado>();
    public DbSet<HorarioAula> HorariosAula => Set<HorarioAula>();
    public DbSet<AulaGrade> Grade => Set<AulaGrade>();
    public DbSet<ProfessorIndisponivel> Indisponibilidades => Set<ProfessorIndisponivel>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<HorarioAula>().ToTable("HorariosAula").HasIndex(x => new { x.Turno, x.Inicio });
        b.Entity<ProfessorIndisponivel>().ToTable("ProfessorIndisponibilidades").HasKey(x => new { x.ProfessorId, x.DiaSemana, x.HorarioAulaId });
        b.Entity<ProfessorIndisponivel>().HasOne<Professor>().WithMany().HasForeignKey(x => x.ProfessorId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ProfessorIndisponivel>().HasOne<HorarioAula>().WithMany().HasForeignKey(x => x.HorarioAulaId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<AulaGrade>().ToTable("GradeAulas").HasKey(x => new { x.TurmaId, x.DiaSemana, x.HorarioAulaId });
        b.Entity<AulaGrade>().HasOne(x => x.Turma).WithMany().HasForeignKey(x => x.TurmaId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<AulaGrade>().HasOne(x => x.Horario).WithMany().HasForeignKey(x => x.HorarioAulaId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AulaGrade>().HasOne(x => x.Disciplina).WithMany().HasForeignKey(x => x.DisciplinaId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Comunicado>().Property(x => x.ValidoAte).HasColumnType("date");
        b.Entity<Comunicado>().Property(x => x.CriadoEm).HasColumnType("timestamp without time zone");
        b.Entity<Comunicado>().Property(x => x.EditadoEm).HasColumnType("timestamp without time zone");
        b.Entity<Comunicado>().Property(x => x.Titulo).HasMaxLength(100);
        b.Entity<Comunicado>().Property(x => x.Texto).HasMaxLength(2000);
        b.Entity<Comunicado>().Property(x => x.Link).HasMaxLength(300);
        b.Entity<Comunicado>().Property(x => x.AutorNome).HasMaxLength(120);
        b.Entity<Comunicado>().HasOne(x => x.Turma).WithMany().HasForeignKey(x => x.TurmaId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<Comunicado>().HasIndex(x => x.ValidoAte);
        b.Entity<LogoEscola>().ToTable("LogosEscola").Property(l => l.Id).ValueGeneratedNever();
        b.Entity<LogoEscola>().Property(l => l.AtualizadoEm).HasColumnType("timestamp without time zone");
        b.Entity<RegistroLog>().ToTable("Registros").HasIndex(r => r.Em);
        b.Entity<RegistroLog>().Property(r => r.Em).HasColumnType("timestamp without time zone");
        b.Entity<Aluno>().Property(a => a.DataNascimento).HasColumnType("date");
        b.Entity<Aluno>().HasIndex(a => a.Cpf).IsUnique();
        b.Entity<Aluno>().HasOne<Usuario>().WithMany().HasForeignKey(a => a.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Aluno>().OwnsOne(a => a.Academico);
        b.Entity<Aluno>().OwnsOne(a => a.Financeiro);
        b.Entity<Turma>().HasIndex(t => new { t.AnoLetivo, t.Serie, t.Letra }).IsUnique();
        b.Entity<TurmaDisciplina>().ToTable("TurmaDisciplinas").HasKey(x => new { x.TurmaId, x.DisciplinaId });
        b.Entity<TurmaDisciplina>().HasOne(x => x.Turma).WithMany(t => t.Disciplinas).HasForeignKey(x => x.TurmaId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<TurmaDisciplina>().HasOne(x => x.Disciplina).WithMany().HasForeignKey(x => x.DisciplinaId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<TurmaDisciplina>().HasOne(x => x.Professor).WithMany().HasForeignKey(x => x.ProfessorId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Matricula>().HasOne(m => m.Turma).WithMany().HasForeignKey(m => m.TurmaId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Evento>().Property(e => e.Inicio).HasColumnType("date");
        b.Entity<Evento>().Property(e => e.Fim).HasColumnType("date");
        b.Entity<Configuracao>().ToTable("Configuracoes").Property(c => c.Id).ValueGeneratedNever();
        b.Entity<Configuracao>().HasData(new Configuracao { Id = 1 });  // padrões: 6,0 / 4,0 / 75% / média das duas / 2% / 1%
        b.Entity<Configuracao>().Property(c => c.MediaMinima).HasPrecision(3, 1);
        b.Entity<Configuracao>().Property(c => c.MediaRecuperacao).HasPrecision(3, 1);
        b.Entity<Configuracao>().Property(c => c.FrequenciaMinima).HasPrecision(4, 1);
        b.Entity<Configuracao>().Property(c => c.MultaPercentual).HasPrecision(5, 2);
        b.Entity<Configuracao>().Property(c => c.JurosMensal).HasPrecision(5, 2);
        b.Entity<Cobranca>().Property(c => c.ValorPago).HasPrecision(10, 2);
        b.Entity<Frequencia>().ToTable("Frequencias").HasKey(f => new { f.MatriculaId, f.DisciplinaId, f.Bimestre });
        b.Entity<Frequencia>().HasOne<Matricula>().WithMany().HasForeignKey(f => f.MatriculaId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Frequencia>().HasOne<Disciplina>().WithMany().HasForeignKey(f => f.DisciplinaId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AulasDadas>().HasOne<Turma>().WithMany().HasForeignKey(a => a.TurmaId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<AulasDadas>().HasOne<Disciplina>().WithMany().HasForeignKey(a => a.DisciplinaId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AulasDadas>().ToTable("AulasDadas").HasKey(a => new { a.TurmaId, a.DisciplinaId, a.Bimestre });
        b.Entity<Publicacao>().ToTable("Publicacoes");
        b.Entity<Publicacao>().Property(x => x.Titulo).HasMaxLength(100);
        b.Entity<Publicacao>().Property(x => x.Texto).HasMaxLength(2000);
        b.Entity<Publicacao>().Property(x => x.Link).HasMaxLength(300);
        b.Entity<Publicacao>().Property(x => x.Prazo).HasColumnType("date");
        b.Entity<Publicacao>().Property(x => x.CriadoEm).HasColumnType("timestamp without time zone");
        b.Entity<Publicacao>().Property(x => x.EditadoEm).HasColumnType("timestamp without time zone");
        b.Entity<Publicacao>().HasOne<Turma>().WithMany().HasForeignKey(x => x.TurmaId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Publicacao>().HasOne(x => x.Disciplina).WithMany().HasForeignKey(x => x.DisciplinaId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Publicacao>().HasOne(x => x.Professor).WithMany().HasForeignKey(x => x.ProfessorId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Publicacao>().HasIndex(x => new { x.TurmaId, x.CriadoEm });
        b.Entity<Cobranca>().ToTable("Cobrancas");
        b.Entity<Cobranca>().Property(c => c.Valor).HasPrecision(10, 2);
        b.Entity<Cobranca>().Property(c => c.Vencimento).HasColumnType("date");
        b.Entity<Cobranca>().Property(c => c.PagoEm).HasColumnType("date");
        b.Entity<Cobranca>().HasOne(c => c.Matricula).WithMany().HasForeignKey(c => c.MatriculaId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Matricula>().Property(m => m.ContratoAssinadoEm).HasColumnType("date");
        b.Entity<Nota>().ToTable("Notas").HasKey(n => new { n.MatriculaId, n.DisciplinaId, n.Bimestre });
        b.Entity<Nota>().Property(n => n.Valor).HasPrecision(4, 1);
        b.Entity<Nota>().HasOne(n => n.Matricula).WithMany().HasForeignKey(n => n.MatriculaId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Nota>().HasOne(n => n.Disciplina).WithMany().HasForeignKey(n => n.DisciplinaId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Disciplina>().HasIndex(d => d.Nome).IsUnique();
        b.Entity<DisciplinaSerie>().ToTable("DisciplinaSeries").HasKey(x => new { x.DisciplinaId, x.Serie });
        b.Entity<Disciplina>().HasMany(d => d.Series).WithOne().HasForeignKey(x => x.DisciplinaId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Professor>().Property(p => p.DataNascimento).HasColumnType("date");
        b.Entity<Professor>().HasIndex(p => p.Cpf).IsUnique();
        b.Entity<Professor>().HasOne(p => p.Usuario).WithMany().HasForeignKey(p => p.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Usuario>().HasIndex(u => u.Email).IsUnique();
        b.Entity<Matricula>().Property(m => m.DataMatricula).HasColumnType("date");
        b.Entity<Matricula>().HasOne(m => m.Aluno).WithMany().HasForeignKey(m => m.AlunoId).IsRequired().OnDelete(DeleteBehavior.Restrict);
        b.Entity<Matricula>().HasIndex(m => new { m.AlunoId, m.AnoLetivo }).IsUnique();
    }
}
