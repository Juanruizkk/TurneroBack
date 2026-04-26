using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TurneroApp.Application.DTOs.Turno;
using TurneroApp.IntegrationTests.Helpers;
using TurneroApp.IntegrationTests.Infrastructure;
using Xunit;

namespace TurneroApp.IntegrationTests.Services;

public class TurnoServiceIntegrationTests : IntegrationTestBase
{
    public TurnoServiceIntegrationTests(PostgresFixture fixture) : base(fixture) { }

    // ─── Test 1: Límite mensual del plan ─────────────────────────────────────

    [Fact]
    public async Task CreateAsync_CuandoLimiteMensualAlcanzado_LanzaInvalidOperationException()
    {
        // Arrange
        var plan     = await SeedHelper.SeedPlanAsync(Db, maxTurnosMes: 2);
        var sucursal = await SeedHelper.SeedSucursalAsync(Db);
        await SeedHelper.SeedSuscripcionAsync(Db, sucursal.Id, plan.Id);
        var cliente  = await SeedHelper.SeedClienteAsync(Db, sucursal.Id);
        var servicio = await SeedHelper.SeedServicioAsync(Db, sucursal.Id);
        var prof     = await SeedHelper.SeedProfesionalAsync(Db);

        var mes = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 5, 10, 0, 0, DateTimeKind.Utc);
        await SeedHelper.SeedTurnoAsync(Db, sucursal.Id, cliente.Id, servicio.Id, prof.Id, mes);
        await SeedHelper.SeedTurnoAsync(Db, sucursal.Id, cliente.Id, servicio.Id, prof.Id, mes.AddHours(2));

        var sut = ServiceFactory.BuildTurnoService(Db);
        var dto = new CreateTurnoDto
        {
            SucursalId = sucursal.Id, ClienteId = cliente.Id,
            ServicioId = servicio.Id, ProfesionalId = prof.Id,
            FechaHoraInicio = mes.AddHours(5),
        };

        // Act
        var act = async () => await sut.CreateAsync(dto);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Actualizá tu suscripción*");

        (await Db.Turnos.CountAsync(t => t.SucursalId == sucursal.Id))
            .Should().Be(2, "el 3er turno no debe haberse persistido");
    }

    // ─── Test 2: Conflicto de horario ────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_CuandoConflictoHorario_LanzaInvalidOperationException()
    {
        // Arrange
        var plan     = await SeedHelper.SeedPlanAsync(Db);   // sin límite
        var sucursal = await SeedHelper.SeedSucursalAsync(Db);
        await SeedHelper.SeedSuscripcionAsync(Db, sucursal.Id, plan.Id);
        var cliente  = await SeedHelper.SeedClienteAsync(Db, sucursal.Id);
        var servicio = await SeedHelper.SeedServicioAsync(Db, sucursal.Id, duracionAgendaMin: 30);
        var prof     = await SeedHelper.SeedProfesionalAsync(Db);

        var inicio = new DateTime(2026, 6, 10, 10, 0, 0, DateTimeKind.Utc);
        // Turno existente: 10:00 – 10:30
        await SeedHelper.SeedTurnoAsync(Db, sucursal.Id, cliente.Id, servicio.Id, prof.Id, inicio, duracionMin: 30);

        var sut = ServiceFactory.BuildTurnoService(Db);
        var dto = new CreateTurnoDto
        {
            SucursalId = sucursal.Id, ClienteId = cliente.Id,
            ServicioId = servicio.Id, ProfesionalId = prof.Id,
            FechaHoraInicio = inicio.AddMinutes(15), // 10:15 — solapa el existente
        };

        // Act
        var act = async () => await sut.CreateAsync(dto);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*ya tiene un turno en el horario seleccionado*");

        (await Db.Turnos.CountAsync(t => t.SucursalId == sucursal.Id))
            .Should().Be(1, "el turno en conflicto no debe haberse persistido");
    }

    // ─── Test 3: Happy path — persistencia real ───────────────────────────────

    [Fact]
    public async Task CreateAsync_CuandoDatosValidos_PersisteTurnoConCamposCorrectos()
    {
        // Arrange
        var plan     = await SeedHelper.SeedPlanAsync(Db);
        var sucursal = await SeedHelper.SeedSucursalAsync(Db);
        await SeedHelper.SeedSuscripcionAsync(Db, sucursal.Id, plan.Id);
        var cliente  = await SeedHelper.SeedClienteAsync(Db, sucursal.Id);
        var servicio = await SeedHelper.SeedServicioAsync(Db, sucursal.Id, duracionAgendaMin: 45);
        var prof     = await SeedHelper.SeedProfesionalAsync(Db);

        var inicio = new DateTime(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);
        var sut    = ServiceFactory.BuildTurnoService(Db);
        var dto    = new CreateTurnoDto
        {
            SucursalId = sucursal.Id, ClienteId = cliente.Id,
            ServicioId = servicio.Id, ProfesionalId = prof.Id,
            FechaHoraInicio = inicio,
        };

        // Act
        var result = await sut.CreateAsync(dto);

        // Assert — DTO de respuesta
        result.Should().NotBeNull();
        result.ClienteNombre.Should().NotBeEmpty();

        // Assert — persistencia real en DB
        var turnoEnDb = await Db.Turnos.SingleAsync(t => t.SucursalId == sucursal.Id);
        turnoEnDb.ClienteId.Should().Be(cliente.Id);
        turnoEnDb.ServicioId.Should().Be(servicio.Id);
        turnoEnDb.ProfesionalId.Should().Be(prof.Id);
        turnoEnDb.FechaHoraFin.Should().Be(inicio.AddMinutes(45));
        turnoEnDb.TokenGestion.Should().NotBeEmpty();
    }
}
