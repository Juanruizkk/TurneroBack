using FluentAssertions;
using TurneroApp.Infrastructure.Repositories;
using TurneroApp.IntegrationTests.Helpers;
using TurneroApp.IntegrationTests.Infrastructure;
using Xunit;

namespace TurneroApp.IntegrationTests.Services;

/// <summary>
/// Tests de integración para los Global Query Filters de EF Core.
/// Verifican que los filtros de Activo/Activa funcionen correctamente en
/// los repositorios genéricos después de las correcciones M-04 y M-05.
/// </summary>
public class QueryFiltersTests : IntegrationTestBase
{
    public QueryFiltersTests(PostgresFixture fixture) : base(fixture) { }

    // ── M-04: HasQueryFilter aplica en GetAllAsync ─────────────────────────

    /// <summary>
    /// Test 14 — Un servicio con Activo=false NO debe aparecer en GetAllAsync.
    /// Verifica que el Global Query Filter 'e.Activo' configurado en AppDbContext
    /// filtra automáticamente las entidades inactivas en listados.
    /// </summary>
    [Fact]
    public async Task GetAllAsync_ServicioInactivo_NoAparece()
    {
        // Arrange
        var sucursal = await SeedHelper.SeedSucursalAsync(Db);
        var activo   = await SeedHelper.SeedServicioAsync(Db, sucursal.Id, activo: true);
        var inactivo = await SeedHelper.SeedServicioAsync(Db, sucursal.Id, activo: false);

        // Act
        var repo  = new ServicioRepository(Db);
        var todos = await repo.GetAllAsync();

        // Assert
        todos.Should().Contain(s => s.Id == activo.Id,
            "el servicio activo debe aparecer en el listado");
        todos.Should().NotContain(s => s.Id == inactivo.Id,
            "el Global Query Filter 'Activo=true' excluye automáticamente los inactivos");
    }

    /// <summary>
    /// Test 14b — Un cliente con Activo=false NO debe aparecer en GetAllAsync.
    /// El mismo filtro aplica a la entidad Cliente.
    /// </summary>
    [Fact]
    public async Task GetAllAsync_ClienteInactivo_NoAparece()
    {
        // Arrange
        var sucursal = await SeedHelper.SeedSucursalAsync(Db);
        var activo   = await SeedHelper.SeedClienteAsync(Db, sucursal.Id, activo: true);
        var inactivo = await SeedHelper.SeedClienteAsync(Db, sucursal.Id, activo: false);

        // Act
        var repo  = new ClienteRepository(Db);
        var todos = await repo.GetAllAsync();

        // Assert
        todos.Should().Contain(c => c.Id == activo.Id);
        todos.Should().NotContain(c => c.Id == inactivo.Id,
            "el Global Query Filter 'Activo=true' excluye clientes inactivos");
    }

    // ── M-05: GetByIdAsync respeta el Query Filter ─────────────────────────

    /// <summary>
    /// Test 15 — GetByIdAsync de un servicio con Activo=false debe devolver null.
    /// Verifica que FirstOrDefaultAsync (fix de M-05) respeta el Query Filter,
    /// a diferencia de FindAsync (código anterior) que lo bypaseaba.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_ServicioInactivo_DevuelveNull()
    {
        // Arrange
        var sucursal = await SeedHelper.SeedSucursalAsync(Db);
        var inactivo = await SeedHelper.SeedServicioAsync(Db, sucursal.Id, activo: false);

        // Act
        var repo   = new ServicioRepository(Db);
        var result = await repo.GetByIdAsync(inactivo.Id);

        // Assert
        result.Should().BeNull(
            "FirstOrDefaultAsync respeta el Query Filter Activo=true; " +
            "FindAsync (antes del fix M-05) lo bypaseaba devolviendo la entidad inactiva");
    }

    /// <summary>
    /// Test 16 — Regresión: GetByIdAsync de un servicio activo debe encontrarlo.
    /// El fix de M-05 no debe romper el caso normal.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_ServicioActivo_LoEncuentra()
    {
        // Arrange
        var sucursal = await SeedHelper.SeedSucursalAsync(Db);
        var activo   = await SeedHelper.SeedServicioAsync(Db, sucursal.Id, activo: true);

        // Act
        var repo   = new ServicioRepository(Db);
        var result = await repo.GetByIdAsync(activo.Id);

        // Assert
        result.Should().NotBeNull(
            "GetByIdAsync debe encontrar entidades activas");
        result!.Id.Should().Be(activo.Id);
    }

    /// <summary>
    /// Test 16b — Regresión: GetByIdAsync de un cliente activo debe encontrarlo.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_ClienteInactivo_DevuelveNull()
    {
        // Arrange
        var sucursal = await SeedHelper.SeedSucursalAsync(Db);
        var inactivo = await SeedHelper.SeedClienteAsync(Db, sucursal.Id, activo: false);

        // Act
        var repo   = new ClienteRepository(Db);
        var result = await repo.GetByIdAsync(inactivo.Id);

        // Assert
        result.Should().BeNull(
            "cliente inactivo es invisible para GetByIdAsync con el fix de M-05");
    }
}
