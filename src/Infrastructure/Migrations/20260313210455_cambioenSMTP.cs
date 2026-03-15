using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class cambioenSMTP : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_usuarios_InvitacionToken",
                table: "usuarios");

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_InvitacionToken",
                table: "usuarios",
                column: "InvitacionToken",
                unique: true,
                filter: "\"InvitacionToken\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_usuarios_InvitacionToken",
                table: "usuarios");

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_InvitacionToken",
                table: "usuarios",
                column: "InvitacionToken",
                unique: true,
                filter: "\"invitacion_token\" IS NOT NULL");
        }
    }
}
