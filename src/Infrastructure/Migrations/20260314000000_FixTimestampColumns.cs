using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TurneroApp.Infrastructure.Data;

#nullable disable

namespace TurneroApp.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260314000000_FixTimestampColumns")]
    public partial class FixTimestampColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<System.DateTime>(
                name: "FechaHoraInicio",
                table: "turnos",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(System.DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<System.DateTime>(
                name: "FechaHoraFin",
                table: "turnos",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(System.DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<System.DateTime>(
                name: "Desde",
                table: "bloqueos_agenda",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(System.DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<System.DateTime>(
                name: "Hasta",
                table: "bloqueos_agenda",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(System.DateTime),
                oldType: "timestamp with time zone");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<System.DateTime>(
                name: "FechaHoraInicio",
                table: "turnos",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(System.DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<System.DateTime>(
                name: "FechaHoraFin",
                table: "turnos",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(System.DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<System.DateTime>(
                name: "Desde",
                table: "bloqueos_agenda",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(System.DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<System.DateTime>(
                name: "Hasta",
                table: "bloqueos_agenda",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(System.DateTime),
                oldType: "timestamp without time zone");
        }
    }
}
