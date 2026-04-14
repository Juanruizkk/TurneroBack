using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TurneroApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanesYSuscripciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "planes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    MaxUsuarios = table.Column<int>(type: "integer", nullable: false),
                    MaxTurnosMes = table.Column<int>(type: "integer", nullable: true),
                    PrecioMensual = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    EsActivo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    OrdenDisplay = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_planes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sucursal_suscripciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SucursalId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    FechaVencimiento = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    EnPeriodoPrueba = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sucursal_suscripciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sucursal_suscripciones_planes_PlanId",
                        column: x => x.PlanId,
                        principalTable: "planes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sucursal_suscripciones_sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "planes",
                columns: new[] { "Id", "Descripcion", "EsActivo", "MaxTurnosMes", "MaxUsuarios", "Nombre", "OrdenDisplay", "PrecioMensual" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "Ideal para comenzar. 2 usuarios, 200 turnos/mes.", true, 200, 2, "Básico", 1, 0m },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "Para negocios en crecimiento. 10 usuarios, turnos ilimitados.", true, null, 10, "Pro", 2, 0m },
                    { new Guid("10000000-0000-0000-0000-000000000003"), "Sin límites. Soporte dedicado.", true, null, 2147483647, "Enterprise", 3, 0m }
                });

            migrationBuilder.CreateIndex(
                name: "IX_sucursal_suscripciones_PlanId",
                table: "sucursal_suscripciones",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_sucursal_suscripciones_SucursalId",
                table: "sucursal_suscripciones",
                column: "SucursalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sucursal_suscripciones");

            migrationBuilder.DropTable(
                name: "planes");
        }
    }
}
