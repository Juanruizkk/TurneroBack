using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sucursales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Direccion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Telefono = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Activa = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sucursales", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cajas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SucursalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Activa = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cajas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cajas_sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "clientes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SucursalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Apellido = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Telefono = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clientes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_clientes_sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "configuraciones_sucursal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SucursalId = table.Column<Guid>(type: "uuid", nullable: false),
                    MinutosAnticipacionMinimaReserva = table.Column<int>(type: "integer", nullable: false, defaultValue: 60),
                    DiasMaximosReservaFutura = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    MinutosBufferEntreTurnos = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    PermiteCancelacionCliente = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    HorasMinimasCancelacion = table.Column<int>(type: "integer", nullable: false, defaultValue: 24),
                    EnviaRecordatorioEmail = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    EnviaRecordatorioWhatsapp = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuraciones_sucursal", x => x.Id);
                    table.ForeignKey(
                        name: "FK_configuraciones_sucursal_sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "servicios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SucursalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Aclaraciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Precio = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DuracionClienteMin = table.Column<int>(type: "integer", nullable: false),
                    DuracionAgendaMin = table.Column<int>(type: "integer", nullable: false),
                    RequiereSena = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TipoSena = table.Column<int>(type: "integer", nullable: true),
                    ValorSena = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ModalidadPrestacion = table.Column<int>(type: "integer", nullable: false),
                    TipoServicio = table.Column<int>(type: "integer", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_servicios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_servicios_sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SucursalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Apellido = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Telefono = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Rol = table.Column<int>(type: "integer", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    Permisos = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_usuarios_sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bloqueos_agenda",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SucursalId = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    Desde = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hasta = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bloqueos_agenda", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bloqueos_agenda_sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bloqueos_agenda_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "horarios_laborales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    DiaSemana = table.Column<int>(type: "integer", nullable: false),
                    HoraDesde = table.Column<TimeSpan>(type: "interval", nullable: false),
                    HoraHasta = table.Column<TimeSpan>(type: "interval", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_horarios_laborales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_horarios_laborales_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "turnos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SucursalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServicioId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfesionalId = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaHoraInicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaHoraFin = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    Origen = table.Column<int>(type: "integer", nullable: false),
                    Observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TokenGestion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PrecioServicio = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MontoSena = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    TotalCobrado = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_turnos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_turnos_clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_turnos_servicios_ServicioId",
                        column: x => x.ServicioId,
                        principalTable: "servicios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_turnos_sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_turnos_usuarios_ProfesionalId",
                        column: x => x.ProfesionalId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "adjuntos_turno",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TurnoId = table.Column<Guid>(type: "uuid", nullable: false),
                    AutorUsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    UrlArchivo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_adjuntos_turno", x => x.Id);
                    table.ForeignKey(
                        name: "FK_adjuntos_turno_turnos_TurnoId",
                        column: x => x.TurnoId,
                        principalTable: "turnos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_adjuntos_turno_usuarios_AutorUsuarioId",
                        column: x => x.AutorUsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "comentarios_turno",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TurnoId = table.Column<Guid>(type: "uuid", nullable: false),
                    AutorUsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Texto = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comentarios_turno", x => x.Id);
                    table.ForeignKey(
                        name: "FK_comentarios_turno_turnos_TurnoId",
                        column: x => x.TurnoId,
                        principalTable: "turnos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_comentarios_turno_usuarios_AutorUsuarioId",
                        column: x => x.AutorUsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "movimientos_caja",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CajaId = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    TurnoId = table.Column<Guid>(type: "uuid", nullable: true),
                    TipoMovimiento = table.Column<int>(type: "integer", nullable: false),
                    MedioPago = table.Column<int>(type: "integer", nullable: false),
                    Monto = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movimientos_caja", x => x.Id);
                    table.ForeignKey(
                        name: "FK_movimientos_caja_cajas_CajaId",
                        column: x => x.CajaId,
                        principalTable: "cajas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_movimientos_caja_turnos_TurnoId",
                        column: x => x.TurnoId,
                        principalTable: "turnos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_movimientos_caja_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pagos_turno",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TurnoId = table.Column<Guid>(type: "uuid", nullable: false),
                    MovimientoCajaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Concepto = table.Column<int>(type: "integer", nullable: false),
                    Monto = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pagos_turno", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pagos_turno_movimientos_caja_MovimientoCajaId",
                        column: x => x.MovimientoCajaId,
                        principalTable: "movimientos_caja",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_pagos_turno_turnos_TurnoId",
                        column: x => x.TurnoId,
                        principalTable: "turnos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_adjuntos_turno_AutorUsuarioId",
                table: "adjuntos_turno",
                column: "AutorUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_adjuntos_turno_TurnoId",
                table: "adjuntos_turno",
                column: "TurnoId");

            migrationBuilder.CreateIndex(
                name: "IX_bloqueos_agenda_SucursalId_Desde_Hasta",
                table: "bloqueos_agenda",
                columns: new[] { "SucursalId", "Desde", "Hasta" });

            migrationBuilder.CreateIndex(
                name: "IX_bloqueos_agenda_UsuarioId",
                table: "bloqueos_agenda",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_cajas_SucursalId",
                table: "cajas",
                column: "SucursalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_clientes_SucursalId_Email",
                table: "clientes",
                columns: new[] { "SucursalId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_clientes_SucursalId_Telefono",
                table: "clientes",
                columns: new[] { "SucursalId", "Telefono" });

            migrationBuilder.CreateIndex(
                name: "IX_comentarios_turno_AutorUsuarioId",
                table: "comentarios_turno",
                column: "AutorUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_comentarios_turno_TurnoId",
                table: "comentarios_turno",
                column: "TurnoId");

            migrationBuilder.CreateIndex(
                name: "IX_configuraciones_sucursal_SucursalId",
                table: "configuraciones_sucursal",
                column: "SucursalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_horarios_laborales_UsuarioId",
                table: "horarios_laborales",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_movimientos_caja_CajaId_Fecha",
                table: "movimientos_caja",
                columns: new[] { "CajaId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_movimientos_caja_TurnoId",
                table: "movimientos_caja",
                column: "TurnoId");

            migrationBuilder.CreateIndex(
                name: "IX_movimientos_caja_UsuarioId",
                table: "movimientos_caja",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_pagos_turno_MovimientoCajaId",
                table: "pagos_turno",
                column: "MovimientoCajaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pagos_turno_TurnoId",
                table: "pagos_turno",
                column: "TurnoId");

            migrationBuilder.CreateIndex(
                name: "IX_servicios_SucursalId",
                table: "servicios",
                column: "SucursalId");

            migrationBuilder.CreateIndex(
                name: "IX_turnos_ClienteId",
                table: "turnos",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_turnos_ProfesionalId_FechaHoraInicio",
                table: "turnos",
                columns: new[] { "ProfesionalId", "FechaHoraInicio" });

            migrationBuilder.CreateIndex(
                name: "IX_turnos_ServicioId",
                table: "turnos",
                column: "ServicioId");

            migrationBuilder.CreateIndex(
                name: "IX_turnos_SucursalId_FechaHoraInicio",
                table: "turnos",
                columns: new[] { "SucursalId", "FechaHoraInicio" });

            migrationBuilder.CreateIndex(
                name: "IX_turnos_TokenGestion",
                table: "turnos",
                column: "TokenGestion",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_SucursalId_Email",
                table: "usuarios",
                columns: new[] { "SucursalId", "Email" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "adjuntos_turno");

            migrationBuilder.DropTable(
                name: "bloqueos_agenda");

            migrationBuilder.DropTable(
                name: "comentarios_turno");

            migrationBuilder.DropTable(
                name: "configuraciones_sucursal");

            migrationBuilder.DropTable(
                name: "horarios_laborales");

            migrationBuilder.DropTable(
                name: "pagos_turno");

            migrationBuilder.DropTable(
                name: "movimientos_caja");

            migrationBuilder.DropTable(
                name: "cajas");

            migrationBuilder.DropTable(
                name: "turnos");

            migrationBuilder.DropTable(
                name: "clientes");

            migrationBuilder.DropTable(
                name: "servicios");

            migrationBuilder.DropTable(
                name: "usuarios");

            migrationBuilder.DropTable(
                name: "sucursales");
        }
    }
}
