using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClienteArchivadoYUniqueTelefono : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_clientes_SucursalId_Telefono",
                table: "clientes");

            migrationBuilder.AddColumn<bool>(
                name: "Activo",
                table: "clientes",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaArchivado",
                table: "clientes",
                type: "timestamp with time zone",
                nullable: true);

            // Nota: el índice se recrea como no-único para permitir datos históricos duplicados.
            // La unicidad de teléfono por sucursal se valida en la capa de servicio.
            // Para agregar el constraint de DB, limpiar duplicados y ejecutar una nueva migración.
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
                name: "Activo",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "FechaArchivado",
                table: "clientes");

            migrationBuilder.CreateIndex(
                name: "IX_clientes_SucursalId_Telefono",
                table: "clientes",
                columns: new[] { "SucursalId", "Telefono" });
        }
    }
}
