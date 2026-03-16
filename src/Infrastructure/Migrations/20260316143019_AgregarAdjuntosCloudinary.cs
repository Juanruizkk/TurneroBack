using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarAdjuntosCloudinary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Descripcion",
                table: "adjuntos_turno");

            migrationBuilder.AlterColumn<string>(
                name: "UrlArchivo",
                table: "adjuntos_turno",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AddColumn<string>(
                name: "NombreArchivo",
                table: "adjuntos_turno",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PublicId",
                table: "adjuntos_turno",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TipoRecurso",
                table: "adjuntos_turno",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "image");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NombreArchivo",
                table: "adjuntos_turno");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "adjuntos_turno");

            migrationBuilder.DropColumn(
                name: "TipoRecurso",
                table: "adjuntos_turno");

            migrationBuilder.AlterColumn<string>(
                name: "UrlArchivo",
                table: "adjuntos_turno",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AddColumn<string>(
                name: "Descripcion",
                table: "adjuntos_turno",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }
    }
}
