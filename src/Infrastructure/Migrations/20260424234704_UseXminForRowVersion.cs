using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UseXminForRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Eliminar las columnas bytea "RowVersion" agregadas en AddRowVersion.
            // La concurrencia optimista ahora usa la columna de sistema xmin de PostgreSQL,
            // que se incrementa automáticamente en cada UPDATE sin necesidad de columna extra.
            migrationBuilder.DropColumn(name: "RowVersion", table: "turnos");
            migrationBuilder.DropColumn(name: "RowVersion", table: "servicios");
            migrationBuilder.DropColumn(name: "RowVersion", table: "configuraciones_sucursal");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
        }
    }
}
