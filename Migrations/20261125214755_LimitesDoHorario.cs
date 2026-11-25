using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaEscolar.Migrations
{
    /// <inheritdoc />
    public partial class LimitesDoHorario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxAulasDisciplinaDia",
                table: "Configuracoes",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "MaxAulasSeguidas",
                table: "Configuracoes",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.UpdateData(
                table: "Configuracoes",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "MaxAulasDisciplinaDia", "MaxAulasSeguidas" },
                values: new object[] { 2, 3 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxAulasDisciplinaDia",
                table: "Configuracoes");

            migrationBuilder.DropColumn(
                name: "MaxAulasSeguidas",
                table: "Configuracoes");
        }
    }
}
