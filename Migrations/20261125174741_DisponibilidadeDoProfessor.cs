using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaEscolar.Migrations
{
    /// <inheritdoc />
    public partial class DisponibilidadeDoProfessor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProfessorIndisponibilidades",
                columns: table => new
                {
                    ProfessorId = table.Column<int>(type: "integer", nullable: false),
                    DiaSemana = table.Column<int>(type: "integer", nullable: false),
                    HorarioAulaId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfessorIndisponibilidades", x => new { x.ProfessorId, x.DiaSemana, x.HorarioAulaId });
                    table.ForeignKey(
                        name: "FK_ProfessorIndisponibilidades_HorariosAula_HorarioAulaId",
                        column: x => x.HorarioAulaId,
                        principalTable: "HorariosAula",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProfessorIndisponibilidades_Professores_ProfessorId",
                        column: x => x.ProfessorId,
                        principalTable: "Professores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProfessorIndisponibilidades_HorarioAulaId",
                table: "ProfessorIndisponibilidades",
                column: "HorarioAulaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProfessorIndisponibilidades");
        }
    }
}
