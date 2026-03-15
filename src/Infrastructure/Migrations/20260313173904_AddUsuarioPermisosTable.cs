using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUsuarioPermisosTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Permisos",
                table: "usuarios");

            migrationBuilder.CreateTable(
                name: "usuario_permisos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Permiso = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuario_permisos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_usuario_permisos_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_usuario_permisos_UsuarioId",
                table: "usuario_permisos",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_usuario_permisos_UsuarioId_Permiso",
                table: "usuario_permisos",
                columns: new[] { "UsuarioId", "Permiso" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "usuario_permisos");

            migrationBuilder.AddColumn<string>(
                name: "Permisos",
                table: "usuarios",
                type: "jsonb",
                nullable: false,
                defaultValue: "");
        }
    }
}
