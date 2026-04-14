using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSucursalMercadoPago : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sucursal_mercadopago",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SucursalId = table.Column<Guid>(type: "uuid", nullable: false),
                    MpUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AccessToken = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RefreshToken = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    AccessTokenExpiraEnUtc = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    RefreshTokenExpiraEnUtc = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    FechaConexionUtc = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UltimaActualizacionUtc = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sucursal_mercadopago", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sucursal_mercadopago_sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sucursal_mercadopago_SucursalId",
                table: "sucursal_mercadopago",
                column: "SucursalId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sucursal_mercadopago");
        }
    }
}
