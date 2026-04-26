# 04 — Tests de Query Filters

**Hallazgos cubiertos:** M-04 (Global Query Filters para Activo/Activa) · M-05 (GenericRepository.FindAsync)
**Proyecto:** `TurneroApp.IntegrationTests`
**Carpeta:** `Security/`
**Clase:** `QueryFiltersTests.cs`

---

## Contexto de los hallazgos

### M-04 — Global Query Filters no aplicados en algunas entidades

Las entidades `Servicio`, `Cliente`, `Usuario` (membresía), etc. tienen una propiedad `Activo`
que indica si están habilitadas. Se configuró un Global Query Filter en EF Core para que
todas las queries automáticamente filtren `WHERE Activo = true`:

```csharp
// En AppDbContext.OnModelCreating:
modelBuilder.Entity<Servicio>().HasQueryFilter(s => s.Activo);
modelBuilder.Entity<Cliente>().HasQueryFilter(c => c.Activo);
// etc.
```

El hallazgo M-04 encontró que algunas entidades no tenían este filtro configurado,
permitiendo que datos "borrados lógicamente" aparecieran en listados.

### M-05 — `GenericRepository.GetByIdAsync` usaba `FindAsync` que bypasea query filters

`DbSet.FindAsync(id)` busca primero en el cache del `DbContext` y luego hace una query directa
por PK — **sin aplicar Global Query Filters**. Esto significa que `GetByIdAsync` podía devolver
una entidad marcada como inactiva.

**Corrección aplicada:** reemplazar `FindAsync` por `FirstOrDefaultAsync` con predicado,
que sí respeta los Global Query Filters:

```csharp
// Antes (M-05 — incorrecto):
public async Task<T?> GetByIdAsync(Guid id) =>
    await _db.Set<T>().FindAsync(id);

// Después (correcto):
public async Task<T?> GetByIdAsync(Guid id) =>
    await _db.Set<T>().FirstOrDefaultAsync(e => e.Id == id);
```

---

## `QueryFiltersTests.cs`

```csharp
using FluentAssertions;
using TurneroApp.Infrastructure.Repositories;
using TurneroApp.IntegrationTests.Infrastructure;
using Xunit;

namespace TurneroApp.IntegrationTests.Security;

[Collection("PostgresTests")]
public class QueryFiltersTests : IntegrationTestBase
{
    public QueryFiltersTests(PostgresFixture fixture) : base(fixture) { }

    // ── M-04: Global Query Filters en GetAllAsync ──────────────────────────

    /// <summary>
    /// Test 14 — Un servicio inactivo NO debe aparecer en GetAllAsync.
    /// Verifica que el Global Query Filter Activo=true se aplica en listados.
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
            "el Global Query Filter filtra automáticamente Activo=false");
    }

    /// <summary>
    /// Test 14b — El filtro aplica también a Clientes inactivos.
    /// </summary>
    [Fact]
    public async Task GetAllAsync_ClienteInactivo_NoAparece()
    {
        var sucursal  = await SeedHelper.SeedSucursalAsync(Db);
        var activo    = await SeedHelper.SeedClienteAsync(Db, sucursal.Id, activo: true);
        var inactivo  = await SeedHelper.SeedClienteAsync(Db, sucursal.Id, activo: false);

        var repo  = new ClienteRepository(Db);
        var todos = await repo.GetAllAsync();

        todos.Should().Contain(c => c.Id == activo.Id);
        todos.Should().NotContain(c => c.Id == inactivo.Id,
            "cliente inactivo no debe aparecer en GetAll");
    }

    // ── M-05: GetByIdAsync respeta el Query Filter ─────────────────────────

    /// <summary>
    /// Test 15 — GetByIdAsync de un servicio inactivo debe devolver null.
    /// Verifica que FirstOrDefaultAsync (post-fix) respeta el Query Filter,
    /// a diferencia de FindAsync (anterior) que lo bypaseaba.
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
            "FindAsync (antes del fix) lo bypaseaba y devolvía la entidad inactiva");
    }

    /// <summary>
    /// Test 16 — Regresión: GetByIdAsync de un servicio ACTIVO debe encontrarlo.
    /// Verifica que el fix de M-05 no rompió el caso normal.
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
    /// Test 16b — GetByIdAsync de cliente inactivo → null (misma regresión para Cliente).
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_ClienteInactivo_DevuelveNull()
    {
        var sucursal = await SeedHelper.SeedSucursalAsync(Db);
        var inactivo = await SeedHelper.SeedClienteAsync(Db, sucursal.Id, activo: false);

        var repo   = new ClienteRepository(Db);
        var result = await repo.GetByIdAsync(inactivo.Id);

        result.Should().BeNull(
            "cliente inactivo debe ser invisible para GetByIdAsync");
    }
}
```

---

## Consideraciones de implementación

### `SeedServicioAsync` con flag `activo`

El helper de seed debe permitir crear servicios con `Activo = false` para los tests negativos.
Sin este parámetro, todos los seeds crearían entidades activas y el test 15 no tendría sentido.

```csharp
// En SeedHelper.cs
public static async Task<Servicio> SeedServicioAsync(
    AppDbContext db, Guid sucursalId, bool activo = true)
{
    var servicio = new Servicio
    {
        Id         = Guid.NewGuid(),
        SucursalId = sucursalId,
        Nombre     = $"Servicio-{Guid.NewGuid():N}",
        Precio     = 100m,
        DuracionClienteMin = 30,
        DuracionAgendaMin  = 30,
        Activo     = activo,   // ← respetar el flag
    };
    db.Servicios.Add(servicio);
    await db.SaveChangesAsync();
    return servicio;
}
```

### Bypass intencional del filtro para seed de inactivos

Cuando el test necesita insertar un registro inactivo, `Db.SaveChangesAsync()` funciona
correctamente porque los Global Query Filters solo aplican a **lecturas** (SELECT), no a
escrituras (INSERT/UPDATE). No hay que usar `IgnoreQueryFilters()` para el seed.

### `IgnoreQueryFilters()` — cuándo es válido usarlo

En código de producción, `IgnoreQueryFilters()` solo debe usarse en queries específicas
que explícitamente necesiten ver entidades inactivas (ej: panel de administración de baja lógica,
reactivación de un recurso). Nunca en el repositorio genérico.

### Repositorios afectados por M-05

Verificar que **todos** los repositorios que hereden de `GenericRepository<T>` usen
`FirstOrDefaultAsync` en su `GetByIdAsync`. Si algún repositorio específico tiene su propio
override de `GetByIdAsync`, también debe usar `FirstOrDefaultAsync`.
