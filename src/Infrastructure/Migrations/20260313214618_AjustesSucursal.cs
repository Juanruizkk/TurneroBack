using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AjustesSucursal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FotoUrl",
                table: "sucursales",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Moneda",
                table: "sucursales",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "ARS");

            migrationBuilder.AddColumn<string>(
                name: "Rubros",
                table: "sucursales",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "Slogan",
                table: "sucursales",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HorariosAtencion",
                table: "configuraciones_sucursal",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FotoUrl",
                table: "sucursales");

            migrationBuilder.DropColumn(
                name: "Moneda",
                table: "sucursales");

            migrationBuilder.DropColumn(
                name: "Rubros",
                table: "sucursales");

            migrationBuilder.DropColumn(
                name: "Slogan",
                table: "sucursales");

            migrationBuilder.DropColumn(
                name: "HorariosAtencion",
                table: "configuraciones_sucursal");
        }
    }
}
