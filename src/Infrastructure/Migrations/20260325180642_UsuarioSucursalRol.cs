using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UsuarioSucursalRol : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Drop old FKs ──────────────────────────────────────────────────────
            migrationBuilder.DropForeignKey(
                name: "FK_horarios_laborales_usuarios_UsuarioId",
                table: "horarios_laborales");

            migrationBuilder.DropForeignKey(
                name: "FK_usuario_permisos_usuarios_UsuarioId",
                table: "usuario_permisos");

            migrationBuilder.DropForeignKey(
                name: "FK_usuario_servicios_usuarios_UsuarioId",
                table: "usuario_servicios");

            migrationBuilder.DropForeignKey(
                name: "FK_usuarios_sucursales_SucursalId",
                table: "usuarios");

            // ── 2. Drop old indexes on usuarios ──────────────────────────────────────
            migrationBuilder.DropIndex(
                name: "IX_usuarios_InvitacionToken",
                table: "usuarios");

            migrationBuilder.DropIndex(
                name: "IX_usuarios_SucursalId_Email",
                table: "usuarios");

            // ── 3. Add FechaCreacion to usuarios (before data migration) ─────────────
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaCreacion",
                table: "usuarios",
                type: "timestamptz",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // ── 4. Create new UsuarioSucursalRoles table ─────────────────────────────
            migrationBuilder.CreateTable(
                name: "usuario_sucursal_roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    SucursalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rol = table.Column<int>(type: "integer", nullable: false),
                    EsOwner = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    InvitacionToken = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FechaExpiracionInvitacion = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuario_sucursal_roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_usuario_sucursal_roles_sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_usuario_sucursal_roles_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // ── 5. DATA MIGRATION: populate USR from existing Usuarios ────────────────
            migrationBuilder.Sql(@"
                INSERT INTO usuario_sucursal_roles (
                    ""Id"", ""UsuarioId"", ""SucursalId"", ""Rol"", ""EsOwner"",
                    ""Estado"", ""InvitacionToken"", ""FechaExpiracionInvitacion"", ""FechaCreacion""
                )
                SELECT
                    gen_random_uuid(),
                    ""Id"",
                    ""SucursalId"",
                    ""Rol"",
                    ""EsOwner"",
                    ""Estado"",
                    ""InvitacionToken"",
                    ""FechaExpiracionInvitacion"",
                    NOW()
                FROM usuarios
            ");

            // ── 6. DATA MIGRATION: redirect related tables to USR.Id ─────────────────
            migrationBuilder.Sql(@"
                UPDATE usuario_permisos up
                SET ""UsuarioId"" = usr.""Id""
                FROM usuario_sucursal_roles usr
                WHERE usr.""UsuarioId"" = up.""UsuarioId""
            ");

            migrationBuilder.Sql(@"
                UPDATE horarios_laborales hl
                SET ""UsuarioId"" = usr.""Id""
                FROM usuario_sucursal_roles usr
                WHERE usr.""UsuarioId"" = hl.""UsuarioId""
            ");

            migrationBuilder.Sql(@"
                UPDATE usuario_servicios us
                SET ""UsuarioId"" = usr.""Id""
                FROM usuario_sucursal_roles usr
                WHERE usr.""UsuarioId"" = us.""UsuarioId""
            ");

            // ── 7. Rename columns (values are now correct USR IDs) ───────────────────
            migrationBuilder.RenameColumn(
                name: "UsuarioId",
                table: "usuario_servicios",
                newName: "UsuarioSucursalRolId");

            migrationBuilder.RenameColumn(
                name: "UsuarioId",
                table: "usuario_permisos",
                newName: "UsuarioSucursalRolId");

            migrationBuilder.RenameIndex(
                name: "IX_usuario_permisos_UsuarioId_Permiso",
                table: "usuario_permisos",
                newName: "IX_usuario_permisos_UsuarioSucursalRolId_Permiso");

            migrationBuilder.RenameIndex(
                name: "IX_usuario_permisos_UsuarioId",
                table: "usuario_permisos",
                newName: "IX_usuario_permisos_UsuarioSucursalRolId");

            migrationBuilder.RenameColumn(
                name: "UsuarioId",
                table: "horarios_laborales",
                newName: "UsuarioSucursalRolId");

            migrationBuilder.RenameIndex(
                name: "IX_horarios_laborales_UsuarioId",
                table: "horarios_laborales",
                newName: "IX_horarios_laborales_UsuarioSucursalRolId");

            // ── 8. Drop old columns from usuarios (now safe) ─────────────────────────
            migrationBuilder.DropColumn(
                name: "EsOwner",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "Estado",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "FechaExpiracionInvitacion",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "InvitacionToken",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "Rol",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "SucursalId",
                table: "usuarios");

            // ── 9. Deduplicate usuarios (same email → multiple rows in old model) ───
            // For each duplicate email, keep the row with the lowest Id (canonical).
            // Redirect all FKs from duplicate rows to the canonical one, then delete them.
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    dup RECORD;
                BEGIN
                    FOR dup IN
                        SELECT ""Email"", MIN(""Id""::text)::uuid AS canonical_id
                        FROM usuarios
                        GROUP BY ""Email""
                        HAVING COUNT(*) > 1
                    LOOP
                        -- Redirect usuario_sucursal_roles
                        UPDATE usuario_sucursal_roles
                           SET ""UsuarioId"" = dup.canonical_id
                         WHERE ""UsuarioId"" != dup.canonical_id
                           AND ""UsuarioId"" IN (
                               SELECT ""Id"" FROM usuarios WHERE ""Email"" = dup.""Email""
                           );

                        -- Redirect turnos.ProfesionalId
                        UPDATE turnos
                           SET ""ProfesionalId"" = dup.canonical_id
                         WHERE ""ProfesionalId"" != dup.canonical_id
                           AND ""ProfesionalId"" IN (
                               SELECT ""Id"" FROM usuarios WHERE ""Email"" = dup.""Email""
                           );

                        -- Redirect movimientos_caja.UsuarioId
                        UPDATE movimientos_caja
                           SET ""UsuarioId"" = dup.canonical_id
                         WHERE ""UsuarioId"" != dup.canonical_id
                           AND ""UsuarioId"" IN (
                               SELECT ""Id"" FROM usuarios WHERE ""Email"" = dup.""Email""
                           );

                        -- Redirect comentarios_turno.AutorUsuarioId
                        UPDATE comentarios_turno
                           SET ""AutorUsuarioId"" = dup.canonical_id
                         WHERE ""AutorUsuarioId"" != dup.canonical_id
                           AND ""AutorUsuarioId"" IN (
                               SELECT ""Id"" FROM usuarios WHERE ""Email"" = dup.""Email""
                           );

                        -- Redirect adjuntos_turno.AutorUsuarioId
                        UPDATE adjuntos_turno
                           SET ""AutorUsuarioId"" = dup.canonical_id
                         WHERE ""AutorUsuarioId"" != dup.canonical_id
                           AND ""AutorUsuarioId"" IN (
                               SELECT ""Id"" FROM usuarios WHERE ""Email"" = dup.""Email""
                           );

                        -- Redirect bloqueos_agenda.UsuarioId
                        UPDATE bloqueos_agenda
                           SET ""UsuarioId"" = dup.canonical_id
                         WHERE ""UsuarioId"" != dup.canonical_id
                           AND ""UsuarioId"" IN (
                               SELECT ""Id"" FROM usuarios WHERE ""Email"" = dup.""Email""
                           );

                        -- Delete non-canonical duplicate rows
                        DELETE FROM usuarios
                         WHERE ""Email"" = dup.""Email""
                           AND ""Id"" != dup.canonical_id;
                    END LOOP;
                END $$;
            ");

            // ── 10. Create indexes ────────────────────────────────────────────────────
            migrationBuilder.CreateIndex(
                name: "IX_usuarios_Email",
                table: "usuarios",
                column: "Email",
                unique: true);

            // ── 11. Create USR indexes ────────────────────────────────────────────────
            migrationBuilder.CreateIndex(
                name: "IX_usuario_sucursal_roles_InvitacionToken",
                table: "usuario_sucursal_roles",
                column: "InvitacionToken",
                unique: true,
                filter: "\"InvitacionToken\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_usuario_sucursal_roles_SucursalId",
                table: "usuario_sucursal_roles",
                column: "SucursalId");

            migrationBuilder.CreateIndex(
                name: "IX_usuario_sucursal_roles_UsuarioId_SucursalId",
                table: "usuario_sucursal_roles",
                columns: new[] { "UsuarioId", "SucursalId" },
                unique: true);

            // ── 12. Add new FKs ───────────────────────────────────────────────────────
            migrationBuilder.AddForeignKey(
                name: "FK_horarios_laborales_usuario_sucursal_roles_UsuarioSucursalRo~",
                table: "horarios_laborales",
                column: "UsuarioSucursalRolId",
                principalTable: "usuario_sucursal_roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_usuario_permisos_usuario_sucursal_roles_UsuarioSucursalRolId",
                table: "usuario_permisos",
                column: "UsuarioSucursalRolId",
                principalTable: "usuario_sucursal_roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_usuario_servicios_usuario_sucursal_roles_UsuarioSucursalRol~",
                table: "usuario_servicios",
                column: "UsuarioSucursalRolId",
                principalTable: "usuario_sucursal_roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_horarios_laborales_usuario_sucursal_roles_UsuarioSucursalRo~",
                table: "horarios_laborales");

            migrationBuilder.DropForeignKey(
                name: "FK_usuario_permisos_usuario_sucursal_roles_UsuarioSucursalRolId",
                table: "usuario_permisos");

            migrationBuilder.DropForeignKey(
                name: "FK_usuario_servicios_usuario_sucursal_roles_UsuarioSucursalRol~",
                table: "usuario_servicios");

            migrationBuilder.DropTable(
                name: "usuario_sucursal_roles");

            migrationBuilder.DropIndex(
                name: "IX_usuarios_Email",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "FechaCreacion",
                table: "usuarios");

            migrationBuilder.RenameColumn(
                name: "UsuarioSucursalRolId",
                table: "usuario_servicios",
                newName: "UsuarioId");

            migrationBuilder.RenameColumn(
                name: "UsuarioSucursalRolId",
                table: "usuario_permisos",
                newName: "UsuarioId");

            migrationBuilder.RenameIndex(
                name: "IX_usuario_permisos_UsuarioSucursalRolId_Permiso",
                table: "usuario_permisos",
                newName: "IX_usuario_permisos_UsuarioId_Permiso");

            migrationBuilder.RenameIndex(
                name: "IX_usuario_permisos_UsuarioSucursalRolId",
                table: "usuario_permisos",
                newName: "IX_usuario_permisos_UsuarioId");

            migrationBuilder.RenameColumn(
                name: "UsuarioSucursalRolId",
                table: "horarios_laborales",
                newName: "UsuarioId");

            migrationBuilder.RenameIndex(
                name: "IX_horarios_laborales_UsuarioSucursalRolId",
                table: "horarios_laborales",
                newName: "IX_horarios_laborales_UsuarioId");

            migrationBuilder.AddColumn<bool>(
                name: "EsOwner",
                table: "usuarios",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Estado",
                table: "usuarios",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaExpiracionInvitacion",
                table: "usuarios",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvitacionToken",
                table: "usuarios",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Rol",
                table: "usuarios",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "SucursalId",
                table: "usuarios",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_InvitacionToken",
                table: "usuarios",
                column: "InvitacionToken",
                unique: true,
                filter: "\"InvitacionToken\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_SucursalId_Email",
                table: "usuarios",
                columns: new[] { "SucursalId", "Email" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_horarios_laborales_usuarios_UsuarioId",
                table: "horarios_laborales",
                column: "UsuarioId",
                principalTable: "usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_usuario_permisos_usuarios_UsuarioId",
                table: "usuario_permisos",
                column: "UsuarioId",
                principalTable: "usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_usuario_servicios_usuarios_UsuarioId",
                table: "usuario_servicios",
                column: "UsuarioId",
                principalTable: "usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_usuarios_sucursales_SucursalId",
                table: "usuarios",
                column: "SucursalId",
                principalTable: "sucursales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
