using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDemoFieldsToSucursal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DemoExpiraEn",
                table: "sucursales",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DemoIp",
                table: "sucursales",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EsDemo",
                table: "sucursales",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_sucursales_demo",
                table: "sucursales",
                columns: new[] { "EsDemo", "DemoExpiraEn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_sucursales_demo",
                table: "sucursales");

            migrationBuilder.DropColumn(
                name: "DemoExpiraEn",
                table: "sucursales");

            migrationBuilder.DropColumn(
                name: "DemoIp",
                table: "sucursales");

            migrationBuilder.DropColumn(
                name: "EsDemo",
                table: "sucursales");
        }
    }
}
