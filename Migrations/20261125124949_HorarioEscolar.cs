using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SistemaEscolar.Migrations
{
    /// <inheritdoc />
    public partial class HorarioEscolar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AulasSemanais",
                table: "TurmaDisciplinas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "HorariosAula",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Turno = table.Column<int>(type: "integer", nullable: false),
                    Inicio = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    Fim = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    Intervalo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HorariosAula", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GradeAulas",
                columns: table => new
                {
                    TurmaId = table.Column<int>(type: "integer", nullable: false),
                    DiaSemana = table.Column<int>(type: "integer", nullable: false),
                    HorarioAulaId = table.Column<int>(type: "integer", nullable: false),
                    DisciplinaId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradeAulas", x => new { x.TurmaId, x.DiaSemana, x.HorarioAulaId });
                    table.ForeignKey(
                        name: "FK_GradeAulas_Disciplinas_DisciplinaId",
                        column: x => x.DisciplinaId,
                        principalTable: "Disciplinas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GradeAulas_HorariosAula_HorarioAulaId",
                        column: x => x.HorarioAulaId,
                        principalTable: "HorariosAula",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GradeAulas_Turmas_TurmaId",
                        column: x => x.TurmaId,
                        principalTable: "Turmas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GradeAulas_DisciplinaId",
                table: "GradeAulas",
                column: "DisciplinaId");

            migrationBuilder.CreateIndex(
                name: "IX_GradeAulas_HorarioAulaId",
                table: "GradeAulas",
                column: "HorarioAulaId");

            migrationBuilder.CreateIndex(
                name: "IX_HorariosAula_Turno_Inicio",
                table: "HorariosAula",
                columns: new[] { "Turno", "Inicio" });

            // Quem já existia: administrador, secretaria e coordenação ganham a área Horários (2048)
            migrationBuilder.Sql("UPDATE \"Usuarios\" SET \"Areas\" = \"Areas\" | 2048 WHERE \"Perfil\" = 1 AND \"Cargo\" IN (0, 1, 3);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GradeAulas");

            migrationBuilder.DropTable(
                name: "HorariosAula");

            migrationBuilder.DropColumn(
                name: "AulasSemanais",
                table: "TurmaDisciplinas");
        }
    }
}
