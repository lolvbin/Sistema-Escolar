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

public static class TabelaNotas
{
    public const int Bimestres = 4;

    public static void Preparar(EscolaContext db)
    {
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "Notas" (
                "MatriculaId" integer NOT NULL REFERENCES "Matriculas" ("Id") ON DELETE CASCADE,
                "DisciplinaId" integer NOT NULL REFERENCES "Disciplinas" ("Id") ON DELETE RESTRICT,
                "Bimestre" integer NOT NULL,
                "Valor" numeric(4,1) NOT NULL,
                PRIMARY KEY ("MatriculaId", "DisciplinaId", "Bimestre")
            );
            CREATE INDEX IF NOT EXISTS "IX_Notas_DisciplinaId" ON "Notas" ("DisciplinaId");
            ALTER TABLE "Alunos" ADD COLUMN IF NOT EXISTS "UsuarioId" integer NULL REFERENCES "Usuarios" ("Id") ON DELETE RESTRICT;
            """);
    }
}
