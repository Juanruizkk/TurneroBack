using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServicioDiasDisponibles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "servicio_dias_disponibles",
                columns: table => new
                {
                    ServicioId = table.Column<Guid>(type: "uuid", nullable: false),
                    DiaSemana = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_servicio_dias_disponibles", x => new { x.ServicioId, x.DiaSemana });
                    table.ForeignKey(
                        name: "FK_servicio_dias_disponibles_servicios_ServicioId",
                        column: x => x.ServicioId,
                        principalTable: "servicios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "servicio_dias_disponibles");
        }
    }
}
