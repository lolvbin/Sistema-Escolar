using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SistemaEscolar.Migrations
{
    /// <inheritdoc />
    public partial class Comunicados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Comunicados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Titulo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Texto = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Link = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Publico = table.Column<int>(type: "integer", nullable: false),
                    TurmaId = table.Column<int>(type: "integer", nullable: true),
                    ValidoAte = table.Column<DateTime>(type: "date", nullable: false),
                    Fixado = table.Column<bool>(type: "boolean", nullable: false),
                    AutorId = table.Column<int>(type: "integer", nullable: true),
                    AutorNome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EditadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comunicados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Comunicados_Turmas_TurmaId",
                        column: x => x.TurmaId,
                        principalTable: "Turmas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Comunicados_TurmaId",
                table: "Comunicados",
                column: "TurmaId");

            migrationBuilder.CreateIndex(
                name: "IX_Comunicados_ValidoAte",
                table: "Comunicados",
                column: "ValidoAte");

            // Quem já existia: administrador, secretaria e coordenação ganham a área Comunicados (1024)
            migrationBuilder.Sql("UPDATE \"Usuarios\" SET \"Areas\" = \"Areas\" | 1024 WHERE \"Perfil\" = 1 AND \"Cargo\" IN (0, 1, 3);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Comunicados");
        }
    }
}
