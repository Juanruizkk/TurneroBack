using FluentAssertions;
using TurneroApp.IntegrationTests.Helpers;
using TurneroApp.IntegrationTests.Infrastructure;
using TurneroApp.Infrastructure.Repositories;
using Xunit;

namespace TurneroApp.IntegrationTests.Services;

/// <summary>
/// Tests de integración que verifican el aislamiento multi-tenant.
/// Cada sucursal debe ser completamente independiente: clientes, servicios,
/// turnos y slots de una sucursal no deben filtrarse en otra.
/// </summary>
public class MultiTenantAislamientoIntegrationTests : IntegrationTestBase
{
    public MultiTenantAislamientoIntegrationTests(PostgresFixture fixture) : base(fixture) { }

    // ─── Test 1: Clientes aislados por sucursal ───────────────────────────────

    [Fact]
    public async Task BuscarClientePorTelefono_NoDevuelveClienteDeOtraSucursal()
    {
        // Arrange
        const string telefono = "3814000001";

        var sucursalA = await SeedHelper.SeedSucursalAsync(Db);
        var sucursalB = await SeedHelper.SeedSucursalAsync(Db);

        // El cliente existe en A
        var clienteA = new TurneroApp.Domain.Entities.Cliente
        {
            Id         = Guid.NewGuid(),
            SucursalId = sucursalA.Id,
            Nombre     = "Ana",
            Apellido   = "García",
            Telefono   = telefono,
        };
        Db.Clientes.Add(clienteA);
        await Db.SaveChangesAsync();

        var repo = new ClienteRepository(Db);

        // Act — buscar el teléfono en la sucursal B
        var resultado = await repo.BuscarPorTelefonoAsync(sucursalB.Id, telefono);

        // Assert
        resultado.Should().BeNull(
            "un cliente de la sucursal A no debe aparecer al buscar en la sucursal B");
    }

    [Fact]
    public async Task BuscarClientePorTelefono_DevuelveClienteDeLaSucursalCorrecta()
    {
        // Arrange — mismo teléfono en dos sucursales distintas (caso real: cliente compartido)
        const string telefono = "3814000002";

        var sucursalA = await SeedHelper.SeedSucursalAsync(Db);
        var sucursalB = await SeedHelper.SeedSucursalAsync(Db);

        var clienteA = new TurneroApp.Domain.Entities.Cliente
        {
            Id = Guid.NewGuid(), SucursalId = sucursalA.Id,
            Nombre = "Luis", Apellido = "Pérez", Telefono = telefono,
        };
        var clienteB = new TurneroApp.Domain.Entities.Cliente
        {
            Id = Guid.NewGuid(), SucursalId = sucursalB.Id,
            Nombre = "Luis", Apellido = "Pérez", Telefono = telefono,
        };
        Db.Clientes.AddRange(clienteA, clienteB);
        await Db.SaveChangesAsync();

        var repo = new ClienteRepository(Db);

        // Act
        var resultadoA = await repo.BuscarPorTelefonoAsync(sucursalA.Id, telefono);
        var resultadoB = await repo.BuscarPorTelefonoAsync(sucursalB.Id, telefono);

        // Assert — cada búsqueda retorna el cliente de su propia sucursal
        resultadoA!.Id.Should().Be(clienteA.Id);
        resultadoB!.Id.Should().Be(clienteB.Id);
    }

    // ─── Test 2: Turnos aislados por sucursal ─────────────────────────────────

    [Fact]
    public async Task GetTurnosPorRango_NoDevuelveTurnosDeOtraSucursal()
    {
        // Arrange
        var sucursalA = await SeedHelper.SeedSucursalAsync(Db);
        var sucursalB = await SeedHelper.SeedSucursalAsync(Db);

        var plan = await SeedHelper.SeedPlanAsync(Db);
        await SeedHelper.SeedSuscripcionAsync(Db, sucursalA.Id, plan.Id);

        var cliente  = await SeedHelper.SeedClienteAsync(Db, sucursalA.Id);
        var servicio = await SeedHelper.SeedServicioAsync(Db, sucursalA.Id);
        var prof     = await SeedHelper.SeedProfesionalAsync(Db);

        var inicio = new DateTime(2027, 3, 1, 10, 0, 0, DateTimeKind.Unspecified);
        await SeedHelper.SeedTurnoAsync(Db, sucursalA.Id, cliente.Id, servicio.Id, prof.Id, inicio);

        var repo = new TurnoRepository(Db);

        // Act — pedir los turnos del mismo rango pero para la sucursal B
        var turnos = await repo.GetActivosSucursalEnRangoAsync(
            sucursalB.Id,
            new DateTime(2027, 3, 1, 0, 0, 0, DateTimeKind.Unspecified),
            new DateTime(2027, 3, 1, 23, 59, 59, DateTimeKind.Unspecified));

        // Assert
        turnos.Should().BeEmpty(
            "los turnos de la sucursal A no deben aparecer en el rango de la sucursal B");
    }

    // ─── Test 3: Servicios aislados por sucursal ──────────────────────────────

    [Fact]
    public async Task GetServiciosBySucursal_NoDevuelveServiciosDeOtraSucursal()
    {
        // Arrange
        var sucursalA = await SeedHelper.SeedSucursalAsync(Db);
        var sucursalB = await SeedHelper.SeedSucursalAsync(Db);

        // 3 servicios en A, 1 en B
        await SeedHelper.SeedServicioAsync(Db, sucursalA.Id);
        await SeedHelper.SeedServicioAsync(Db, sucursalA.Id);
        await SeedHelper.SeedServicioAsync(Db, sucursalA.Id);
        var servicioB = await SeedHelper.SeedServicioAsync(Db, sucursalB.Id);

        var repo = new ServicioRepository(Db);

        // Act
        var serviciosA = await repo.GetBySucursalAsync(sucursalA.Id);
        var serviciosB = await repo.GetBySucursalAsync(sucursalB.Id);

        // Assert
        serviciosA.Should().HaveCount(3,
            "solo los 3 servicios de la sucursal A deben aparecer en su listado");
        serviciosB.Should().HaveCount(1,
            "solo el 1 servicio de la sucursal B debe aparecer en su listado");
        serviciosB.Single().Id.Should().Be(servicioB.Id);
    }

    // ─── Test 4: Slots no contaminados por turnos de otra sucursal ────────────

    [Fact]
    public async Task ObtenerSlots_TurnosDeOtraSucursalNoGeneranConflicto()
    {
        // Arrange
        // Mismo profesional registrado en dos sucursales.
        // La sucursal A tiene un turno a las 09:00 para ese profesional.
        // Al pedir slots para la sucursal B, el slot de las 09:00 debe aparecer libre
        // porque GetActivosSucursalEnRangoAsync filtra por sucursal.
        var horarioJson = SeedHelper.HorarioJson("Lunes", "09:00", "10:00");

        var sucursalA = await SeedHelper.SeedSucursalAsync(Db);
        var sucursalB = await SeedHelper.SeedSucursalAsync(Db);

        await SeedHelper.SeedConfiguracionAsync(Db, sucursalA.Id, horariosJson: horarioJson, anticipacionMin: 0);
        await SeedHelper.SeedConfiguracionAsync(Db, sucursalB.Id, horariosJson: horarioJson, anticipacionMin: 0);

        var prof     = await SeedHelper.SeedProfesionalAsync(Db);
        var servicio = await SeedHelper.SeedServicioAsync(Db, sucursalA.Id, duracionAgendaMin: 30);
        var servicioB = await SeedHelper.SeedServicioAsync(Db, sucursalB.Id, duracionAgendaMin: 30);

        // Prof activo en AMBAS sucursales
        await SeedHelper.SeedUsuarioSucursalRolAsync(Db, sucursalA.Id, prof.Id);
        await SeedHelper.SeedUsuarioSucursalRolAsync(Db, sucursalB.Id, prof.Id);

        // Turno en sucursal A a las 09:00 (bloquea al prof en A)
        var clienteA = await SeedHelper.SeedClienteAsync(Db, sucursalA.Id);
        var inicio   = new DateTime(2027, 3, 1, 9, 0, 0, DateTimeKind.Unspecified);
        await SeedHelper.SeedTurnoAsync(Db, sucursalA.Id, clienteA.Id, servicio.Id, prof.Id,
                                        inicio, duracionMin: 30);

        var sut = ServiceFactory.BuildPublicoService(Db);

        // Act — pedir slots para sucursal B
        var slotsB = await sut.ObtenerSlotsAsync(sucursalB.Slug, new DateOnly(2027, 3, 1), servicioB.Id);
        // Control — pedir slots para sucursal A (el slot 09:00 debe estar bloqueado)
        var slotsA = await sut.ObtenerSlotsAsync(sucursalA.Slug, new DateOnly(2027, 3, 1), servicio.Id);

        // Assert
        slotsB.Should().Contain(s => s.Hora == "09:00",
            "el turno de la sucursal A no debe bloquear el slot en la sucursal B");
        slotsA.Should().NotContain(s => s.Hora == "09:00",
            "el slot de las 09:00 sí debe estar bloqueado en la sucursal A");
    }
}
