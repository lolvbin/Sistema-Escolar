using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.EntityFrameworkCore;

namespace SistemaEscolar.Data;

// Turma = série + ano letivo + letra (ex.: "5º ano A, 2027"), com um turno.
// Os alunos entram pela matrícula (Matricula.TurmaId); cada disciplina da série tem um professor.
public class Turma
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Selecione a série")]
    [Display(Name = "Série")]
    public Serie? Serie { get; set; }

    [Required(ErrorMessage = "Selecione o turno")]
    public Turno? Turno { get; set; }

    [Display(Name = "Ano letivo")]
    public int AnoLetivo { get; set; } = DateTime.Today.Year + 1;

    [Required(ErrorMessage = "Informe a letra da turma")]
    [StringLength(2)]
    [RegularExpression("^[A-Za-z]{1,2}$", ErrorMessage = "Use 1 ou 2 letras (A, B, C...)")]
    [Display(Name = "Letra")]
    public string Letra { get; set; } = "";

    public bool Ativa { get; set; } = true;

    [ValidateNever]
    public List<TurmaDisciplina> Disciplinas { get; set; } = new();

    [NotMapped]
    public string Nome => $"{Serie?.Nome()} {Letra}";
}

public class TurmaDisciplina
{
    public int TurmaId { get; set; }
    public int DisciplinaId { get; set; }
    public int? ProfessorId { get; set; }

    public Turma? Turma { get; set; }
    public Disciplina? Disciplina { get; set; }
    public Professor? Professor { get; set; }
}
