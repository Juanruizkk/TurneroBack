using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TurneroApp.IntegrationTests.Helpers;
using TurneroApp.IntegrationTests.Infrastructure;
using Xunit;

namespace TurneroApp.IntegrationTests.Services;

/// <summary>
/// Tests de integración para la concurrencia optimista con RowVersion (B-02).
/// Cada test usa dos AppDbContext independientes para simular dos usuarios
/// editando el mismo registro simultáneamente.
/// </summary>
public class ConcurrenciaOptimistaTests : IntegrationTestBase
{
    public ConcurrenciaOptimistaTests(PostgresFixture fixture) : base(fixture) { }

    /// <summary>
    /// Test 12 — Dos DbContext leen el mismo Turno. El segundo guarda primero
    /// (incrementa RowVersion a v2). El primero intenta guardar con v1 (obsoleto)
    /// → debe lanzar DbUpdateConcurrencyException.
    /// </summary>
    [Fact]
    public async Task ActualizarTurno_VersionObsoleta_LanzaConcurrencyException()
    {
        // Arrange — seed un turno con todos sus prerequisitos
        var sucursal  = await SeedHelper.SeedSucursalAsync(Db);
        var cliente   = await SeedHelper.SeedClienteAsync(Db, sucursal.Id);
        var servicio  = await SeedHelper.SeedServicioAsync(Db, sucursal.Id);
        var prof      = await SeedHelper.SeedProfesionalAsync(Db);
        var turno     = await SeedHelper.SeedTurnoAsync(
            Db, sucursal.Id, cliente.Id, servicio.Id, prof.Id,
            fechaHoraInicio: DateTime.UtcNow.AddDays(3));

        // Contexto 1 lee el turno (captura RowVersion = v1)
        var db1      = Fixture.CreateDbContext();
        var turnoC1  = await db1.Turnos.FirstOrDefaultAsync(t => t.Id == turno.Id);
        turnoC1.Should().NotBeNull();

        // Contexto 2 también lee y guarda primero → RowVersion se incrementa a v2
        var db2      = Fixture.CreateDbContext();
        var turnoC2  = await db2.Turnos.FirstOrDefaultAsync(t => t.Id == turno.Id);
        turnoC2!.Observaciones = "Guardado por contexto 2";
        await db2.SaveChangesAsync();

        // Contexto 1 intenta guardar con RowVersion = v1 (ya obsoleto)
        turnoC1!.Observaciones = "Guardado por contexto 1";
        var act = async () => await db1.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "el RowVersion de contexto 1 (v1) no coincide con el actual en DB (v2)");

        // Cleanup
        await db1.DisposeAsync();
        await db2.DisposeAsync();
    }

    /// <summary>
    /// Test 13 — Mismo escenario para la entidad Servicio.
    /// Verifica que B-02 aplique a Servicios además de Turnos.
    /// </summary>
    [Fact]
    public async Task ActualizarServicio_VersionObsoleta_LanzaConcurrencyException()
    {
        // Arrange
        var sucursal = await SeedHelper.SeedSucursalAsync(Db);
        var servicio = await SeedHelper.SeedServicioAsync(Db, sucursal.Id, activo: true);

        // Contexto 1 lee el servicio (RowVersion = v1)
        var db1       = Fixture.CreateDbContext();
        var servicioC1 = await db1.Servicios.FirstOrDefaultAsync(s => s.Id == servicio.Id);
        servicioC1.Should().NotBeNull();

        // Contexto 2 actualiza primero (RowVersion = v2)
        var db2       = Fixture.CreateDbContext();
        var servicioC2 = await db2.Servicios.FirstOrDefaultAsync(s => s.Id == servicio.Id);
        servicioC2!.Nombre = "Nombre desde contexto 2";
        await db2.SaveChangesAsync();

        // Contexto 1 intenta actualizar con RowVersion obsoleto
        servicioC1!.Nombre = "Nombre desde contexto 1";
        var act = async () => await db1.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "el RowVersion del contexto 1 ya fue superado por el contexto 2");

        // Cleanup
        await db1.DisposeAsync();
        await db2.DisposeAsync();
    }
}
