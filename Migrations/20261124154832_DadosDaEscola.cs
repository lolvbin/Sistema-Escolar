using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaEscolar.Migrations
{
    /// <inheritdoc />
    public partial class DadosDaEscola : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Endereco",
                table: "Configuracoes",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NomeEscola",
                table: "Configuracoes",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Telefone",
                table: "Configuracoes",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "Configuracoes",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Endereco", "NomeEscola", "Telefone" },
                values: new object[] { "", "Sistema Escolar", "" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Endereco",
                table: "Configuracoes");

            migrationBuilder.DropColumn(
                name: "NomeEscola",
                table: "Configuracoes");

            migrationBuilder.DropColumn(
                name: "Telefone",
                table: "Configuracoes");
        }
    }
}
