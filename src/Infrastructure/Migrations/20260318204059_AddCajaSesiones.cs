using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCajaSesiones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CajaSesionId",
                table: "movimientos_caja",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "caja_sesiones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CajaId = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaApertura = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FechaCierre = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    MontoInicial = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MontoFinalEsperado = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    MontoFinalReal = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Diferencia = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    UsuarioAperturaId = table.Column<Guid>(type: "uuid", nullable: true),
                    UsuarioCierreId = table.Column<Guid>(type: "uuid", nullable: true),
                    ObservacionesApertura = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ObservacionesCierre = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_caja_sesiones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_caja_sesiones_cajas_CajaId",
                        column: x => x.CajaId,
                        principalTable: "cajas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_caja_sesiones_usuarios_UsuarioAperturaId",
                        column: x => x.UsuarioAperturaId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_caja_sesiones_usuarios_UsuarioCierreId",
                        column: x => x.UsuarioCierreId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_movimientos_caja_CajaSesionId",
                table: "movimientos_caja",
                column: "CajaSesionId");

            migrationBuilder.CreateIndex(
                name: "IX_caja_sesiones_CajaId_Estado",
                table: "caja_sesiones",
                columns: new[] { "CajaId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_caja_sesiones_UsuarioAperturaId",
                table: "caja_sesiones",
                column: "UsuarioAperturaId");

            migrationBuilder.CreateIndex(
                name: "IX_caja_sesiones_UsuarioCierreId",
                table: "caja_sesiones",
                column: "UsuarioCierreId");

            migrationBuilder.AddForeignKey(
                name: "FK_movimientos_caja_caja_sesiones_CajaSesionId",
                table: "movimientos_caja",
                column: "CajaSesionId",
                principalTable: "caja_sesiones",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_movimientos_caja_caja_sesiones_CajaSesionId",
                table: "movimientos_caja");

            migrationBuilder.DropTable(
                name: "caja_sesiones");

            migrationBuilder.DropIndex(
                name: "IX_movimientos_caja_CajaSesionId",
                table: "movimientos_caja");

            migrationBuilder.DropColumn(
                name: "CajaSesionId",
                table: "movimientos_caja");
        }
    }
}
