using Microsoft.EntityFrameworkCore;

namespace SistemaEscolar.Data;

// Nota de um aluno (pela matrícula) em uma disciplina, em um bimestre (1 a 4). Valor de 0 a 10.
// Lançada pelo professor atribuído à disciplina na turma (TurmaDisciplina).
public class Nota
{
    public int MatriculaId { get; set; }
    public int DisciplinaId { get; set; }
    public int Bimestre { get; set; }
    public decimal Valor { get; set; }

    public Matricula? Matricula { get; set; }
    public Disciplina? Disciplina { get; set; }
}

// Faltas de um aluno (pela matrícula) em uma disciplina, em um bimestre.
public class Frequencia
{
    public int MatriculaId { get; set; }
    public int DisciplinaId { get; set; }
    public int Bimestre { get; set; }
    public int Faltas { get; set; }
    /// <summary>Faltas justificadas pela escola (atestado etc.): não entram no cálculo da frequência. Registradas pela secretaria/coordenação.</summary>
    public int Abonadas { get; set; }
    [System.ComponentModel.DataAnnotations.MaxLength(200)] public string MotivoAbono { get; set; } = "";
    /// <summary>Faltas que contam contra o aluno (nunca negativo).</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped, Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
    public int Contadas => Math.Max(0, Faltas - Abonadas);
}

// Aulas dadas pela turma em uma disciplina, em um bimestre (base para o % de frequência).
public class AulasDadas
{
    public int TurmaId { get; set; }
    public int DisciplinaId { get; set; }
    public int Bimestre { get; set; }
    public int Aulas { get; set; }
}

public static class TabelaNotas
{
    public const int Bimestres = 4;
    public const int BimRecuperacao = 5; // a nota de recuperação é guardada como "bimestre 5"

}
