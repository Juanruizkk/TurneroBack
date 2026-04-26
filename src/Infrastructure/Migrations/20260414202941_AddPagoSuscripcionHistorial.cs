using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPagoSuscripcionHistorial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxServicios",
                table: "planes");

            migrationBuilder.CreateTable(
                name: "pagos_suscripcion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SucursalId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanNombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Monto = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MPPaymentId = table.Column<long>(type: "bigint", nullable: false),
                    FechaPago = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pagos_suscripcion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pagos_suscripcion_sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pagos_suscripcion_MPPaymentId",
                table: "pagos_suscripcion",
                column: "MPPaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pagos_suscripcion_SucursalId",
                table: "pagos_suscripcion",
                column: "SucursalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pagos_suscripcion");

            migrationBuilder.AddColumn<int>(
                name: "MaxServicios",
                table: "planes",
                type: "integer",
                nullable: true);
        }
    }
}
