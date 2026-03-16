using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordatoriosEmailFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Recordatorio24hEnviado",
                table: "turnos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Recordatorio3hEnviado",
                table: "turnos",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Recordatorio24hEnviado",
                table: "turnos");

            migrationBuilder.DropColumn(
                name: "Recordatorio3hEnviado",
                table: "turnos");
        }
    }
}
