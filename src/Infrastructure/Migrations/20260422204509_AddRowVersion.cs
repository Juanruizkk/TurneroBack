using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_clientes_SucursalId_Telefono",
                table: "clientes");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "turnos",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "servicios",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "configuraciones_sucursal",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_clientes_SucursalId_Telefono",
                table: "clientes",
                columns: new[] { "SucursalId", "Telefono" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_clientes_SucursalId_Telefono",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "turnos");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "servicios");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "configuraciones_sucursal");

            migrationBuilder.CreateIndex(
                name: "IX_clientes_SucursalId_Telefono",
                table: "clientes",
                columns: new[] { "SucursalId", "Telefono" },
                unique: true);
        }
    }
}
