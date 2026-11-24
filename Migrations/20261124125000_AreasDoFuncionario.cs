using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaEscolar.Migrations
{
    /// <inheritdoc />
    public partial class AreasDoFuncionario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Areas",
                table: "Usuarios",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Cargo",
                table: "Usuarios",
                type: "integer",
                nullable: true);

            // Quem já era funcionário vira Administrador (todas as áreas = 1023) para não perder o acesso
            migrationBuilder.Sql("UPDATE \"Usuarios\" SET \"Cargo\" = 0, \"Areas\" = 1023 WHERE \"Perfil\" = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Areas",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "Cargo",
                table: "Usuarios");
        }
    }
}
