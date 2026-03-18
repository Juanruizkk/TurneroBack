using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMercadoPagoFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MensajeWhatsApp24h",
                table: "configuraciones_sucursal",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MensajeWhatsApp3h",
                table: "configuraciones_sucursal",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MensajeWhatsApp24h",
                table: "configuraciones_sucursal");

            migrationBuilder.DropColumn(
                name: "MensajeWhatsApp3h",
                table: "configuraciones_sucursal");
        }
    }
}
