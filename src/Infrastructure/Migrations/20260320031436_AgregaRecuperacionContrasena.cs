using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregaRecuperacionContrasena : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TokenRecuperacion",
                table: "usuarios",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TokenRecuperacionExpiracion",
                table: "usuarios",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_TokenRecuperacion",
                table: "usuarios",
                column: "TokenRecuperacion",
                unique: true,
                filter: "\"TokenRecuperacion\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_usuarios_TokenRecuperacion",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "TokenRecuperacion",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "TokenRecuperacionExpiracion",
                table: "usuarios");
        }
    }
}
