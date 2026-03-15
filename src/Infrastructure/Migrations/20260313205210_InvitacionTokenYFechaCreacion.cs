using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InvitacionTokenYFechaCreacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaExpiracionInvitacion",
                table: "usuarios",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvitacionToken",
                table: "usuarios",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

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

            migrationBuilder.DropColumn(
                name: "FechaExpiracionInvitacion",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "InvitacionToken",
                table: "usuarios");
        }
    }
}
