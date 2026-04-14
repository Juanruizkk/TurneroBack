using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMercadoPagoFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Columnas ya agregadas en AgregarPlantillasWhatsApp (20260316150855).
            // Esta migration quedó duplicada. Se deja vacía para no romper
            // el historial de migrations en DBs existentes.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op correspondiente al Up vacío.
        }
    }
}
