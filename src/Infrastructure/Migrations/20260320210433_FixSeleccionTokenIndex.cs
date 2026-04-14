using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixSeleccionTokenIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_usuarios_SeleccionToken",
                table: "usuarios");

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_SeleccionToken",
                table: "usuarios",
                column: "SeleccionToken",
                filter: "\"SeleccionToken\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_usuarios_SeleccionToken",
                table: "usuarios");

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_SeleccionToken",
                table: "usuarios",
                column: "SeleccionToken",
                unique: true,
                filter: "\"SeleccionToken\" IS NOT NULL");
        }
    }
}
