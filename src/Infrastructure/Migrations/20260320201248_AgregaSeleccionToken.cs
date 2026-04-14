using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregaSeleccionToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SeleccionToken",
                table: "usuarios",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SeleccionTokenExpiracion",
                table: "usuarios",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_SeleccionToken",
                table: "usuarios",
                column: "SeleccionToken",
                unique: true,
                filter: "\"SeleccionToken\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_usuarios_SeleccionToken",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "SeleccionToken",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "SeleccionTokenExpiracion",
                table: "usuarios");
        }
    }
}
