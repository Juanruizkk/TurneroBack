using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregaSlugSucursal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Paso 1: agregar como nullable para no romper filas existentes
            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "sucursales",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            // Paso 2: backfill — derivar slug desde Nombre
            migrationBuilder.Sql(@"
                UPDATE sucursales
                SET ""Slug"" = lower(
                    regexp_replace(
                        regexp_replace(
                            translate(""Nombre"",
                                'áéíóúàèìòùäëïöüâêîôûÁÉÍÓÚÀÈÌÒÙÄËÏÖÜÂÊÎÔÛñÑüÜ',
                                'aeiouaeiouaeiouaeiouAEIOUAEIOUAEIOUAEIOUNnuU'
                            ),
                        '[^a-zA-Z0-9]+', '-', 'g'),
                    '^-+|-+$', '', 'g')
                );
            ");

            // Paso 3: fallback para filas que quedaron con slug NULL o vacío
            migrationBuilder.Sql(@"
                UPDATE sucursales
                SET ""Slug"" = 'sucursal-' || substring(""Id""::text, 1, 8)
                WHERE ""Slug"" IS NULL OR ""Slug"" = '';
            ");

            // Paso 4: deduplicar colisiones de slug (mismo nombre → mismo slug base)
            migrationBuilder.Sql(@"
                WITH dupes AS (
                    SELECT ""Id"",
                           ROW_NUMBER() OVER (PARTITION BY ""Slug"" ORDER BY ""FechaCreacion"") AS rn
                    FROM sucursales
                )
                UPDATE sucursales
                SET ""Slug"" = sucursales.""Slug"" || '-' || substring(sucursales.""Id""::text, 1, 8)
                FROM dupes
                WHERE sucursales.""Id"" = dupes.""Id"" AND dupes.rn > 1;
            ");

            // Paso 5: hacer NOT NULL
            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                table: "sucursales",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldNullable: true);

            // Paso 6: índice único
            migrationBuilder.CreateIndex(
                name: "IX_sucursales_slug",
                table: "sucursales",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_sucursales_slug",
                table: "sucursales");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "sucursales");
        }
    }
}
