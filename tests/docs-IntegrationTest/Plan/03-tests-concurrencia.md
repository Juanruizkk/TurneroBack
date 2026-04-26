# 03 — Tests de Concurrencia

**Hallazgos cubiertos:** M-03 (race condition TOCTOU) · B-02 (RowVersion concurrencia optimista)
**Proyecto:** `TurneroApp.IntegrationTests`
**Carpeta:** `Security/`
**Clases:** `ConflictoHorarioConcurrenteTests.cs` · `ConcurrenciaOptimistaTests.cs`

---

## M-03 — Race Condition TOCTOU en creación de turnos

### Contexto del hallazgo

El flujo original de `TurnoService.CrearAsync` era:

```
1. ExisteConflictoHorarioAsync(...)  // SELECT
2. // ... tiempo entre el check y el insert ...
3. _repo.AddAsync(turno)             // INSERT
```

Entre el paso 1 y el 3, otro request concurrente podía superar el mismo check y también
hacer el INSERT, resultando en dos turnos para el mismo profesional/horario.

### Corrección aplicada

Se envolvió el check + insert en una transacción con nivel de aislamiento `Serializable`:

```csharp
await using var tx = await _db.Database.BeginTransactionAsync(
    IsolationLevel.Serializable);
// ... check + insert ...
await tx.CommitAsync();
```

PostgreSQL detecta el conflicto de serialización y lanza `PostgresException` con código `40001`
en el segundo commit. El servicio captura esto y lanza `InvalidOperationException("Conflicto de horario")`,
que el controller traduce a `409 Conflict`.

### Por qué HTTP y no servicio directo

Con el nivel de aislamiento `Serializable`, Postgres necesita que cada transacción use
su propia **conexión** de base de datos. Si instanciamos el servicio directamente en el test
y llamamos a `CrearAsync` dos veces en el mismo thread, ambas llamadas usan el mismo
`DbContext` y el mismo connection pool, lo que no replica el escenario de producción.

Con `Task.WhenAll` sobre `HttpClient.PostAsync`, cada request HTTP obtiene su propio scope
de DI y por lo tanto su propio `DbContext` con conexión independiente al pool de Npgsql.
Esto replica exactamente el comportamiento de dos usuarios simultáneos en producción.

---

## `ConflictoHorarioConcurrenteTests.cs`

```csharp
using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TurneroApp.IntegrationTests.Infrastructure;
using Xunit;

namespace TurneroApp.IntegrationTests.Security;

[Collection("HttpTests")]
public class ConflictoHorarioConcurrenteTests : HttpIntegrationTestBase
{
    public ConflictoHorarioConcurrenteTests(HttpTestFixture factory) : base(factory) { }

    /// <summary>
    /// Test 11 — 5 requests simultáneos para el mismo slot de turno.
    /// Solo 1 debe persistirse (201); el resto deben recibir 409 Conflict.
    /// Verifica que la transacción Serializable de M-03 sea efectiva.
    /// </summary>
    [Fact]
    public async Task CrearTurno_ConcurrenteMismoHorario_SoloUnoSePersiste()
    {
        // Arrange
        var (sucursalId, clienteId, servicioId, profId) =
            await SeedTurnoPrerequisitosAsync();

        var inicio = DateTime.UtcNow.AddDays(5).Date.AddHours(10); // slot fijo

        // 5 clientes HTTP independientes → 5 DbContext distintos en el servidor
        var tasks = Enumerable.Range(0, 5).Select(_ =>
        {
            var client  = Factory.CreateClient(); // cliente nuevo por request
            var payload = JsonSerializer.Serialize(new
            {
                sucursalId,
                clienteId,
                servicioId,
                profesionalId   = profId,
                fechaHoraInicio = inicio,
            });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            return client.PostAsync("/api/turnos", content);
        });

        // Act — lanzar todos simultáneamente
        var responses = await Task.WhenAll(tasks);

        // Assert — exactamente 1 ganador, 4 rechazados
        var creados    = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflictos = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        creados.Should().Be(1,
            "la transacción Serializable solo permite que un request gane la carrera");
        conflictos.Should().Be(4,
            "los otros 4 requests deben recibir 409 Conflict");

        // Y en la DB: exactamente 1 turno para ese profesional en ese horario
        var turnosEnDb = await Db.Turnos
            .CountAsync(t => t.ProfesionalId == profId &&
                             t.FechaHoraInicio == inicio);
        turnosEnDb.Should().Be(1,
            "solo debe haber un turno persistido en la base de datos");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<(Guid sucursalId, Guid clienteId, Guid servicioId, Guid profId)>
        SeedTurnoPrerequisitosAsync()
    {
        var sucursal = await SeedHelper.SeedSucursalAsync(Db);
        var cliente  = await SeedHelper.SeedClienteAsync(Db, sucursal.Id);
        var servicio = await SeedHelper.SeedServicioAsync(Db, sucursal.Id, activo: true);
        var prof     = await SeedHelper.SeedProfesionalAsync(Db, sucursal.Id);
        return (sucursal.Id, cliente.Id, servicio.Id, prof.UsuarioId);
    }
}
```

---

## B-02 — RowVersion / Concurrencia Optimista

### Contexto del hallazgo

Sin `RowVersion`, dos usuarios podían editar el mismo turno o servicio simultáneamente y el
segundo `SaveChanges` silenciosamente sobreescribía los cambios del primero (last-write-wins).

### Corrección aplicada

Se agregó la propiedad `RowVersion` a las entidades afectadas y se configuró en EF Core como
token de concurrencia:

```csharp
// Entidad
public uint RowVersion { get; set; }

// Configuración Fluent API
builder.Property(e => e.RowVersion)
       .IsRowVersion()
       .HasColumnType("xid")        // tipo nativo de Postgres para versiones de fila
       .ValueGeneratedOnAddOrUpdate();
```

Con esto, EF Core agrega `WHERE RowVersion = @originalVersion` en los `UPDATE`. Si el valor
cambió entre el `SELECT` y el `UPDATE`, Postgres no encuentra la fila y EF lanza
`DbUpdateConcurrencyException`.

---

## `ConcurrenciaOptimistaTests.cs`

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TurneroApp.IntegrationTests.Infrastructure;
using Xunit;

namespace TurneroApp.IntegrationTests.Security;

[Collection("PostgresTests")]
public class ConcurrenciaOptimistaTests : IntegrationTestBase
{
    public ConcurrenciaOptimistaTests(PostgresFixture fixture) : base(fixture) { }

    /// <summary>
    /// Test 12 — Dos DbContext leen el mismo Turno. El contexto 2 guarda primero
    /// (incrementa RowVersion). El contexto 1 intenta guardar con RowVersion obsoleto
    /// → debe lanzar DbUpdateConcurrencyException.
    /// </summary>
    [Fact]
    public async Task ActualizarTurno_VersionObsoleta_LanzaConcurrencyException()
    {
        // Arrange — seed un turno
        var turno = await SeedTurnoAsync();

        // Contexto 1 lee el turno (captura RowVersion = v1)
        var db1      = Fixture.CreateDbContext();
        var turnoC1  = await db1.Turnos.FindAsync(turno.Id);
        turnoC1.Should().NotBeNull();

        // Contexto 2 también lee y actualiza primero → RowVersion se incrementa a v2
        var db2      = Fixture.CreateDbContext();
        var turnoC2  = await db2.Turnos.FindAsync(turno.Id);
        turnoC2!.Observaciones = "Guardado por contexto 2";
        await db2.SaveChangesAsync(); // RowVersion ahora = v2

        // Contexto 1 intenta guardar con RowVersion = v1 (ya obsoleto)
        turnoC1!.Observaciones = "Guardado por contexto 1";
        var act = async () => await db1.SaveChangesAsync();

        // Assert — EF Core debe lanzar DbUpdateConcurrencyException
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "el RowVersion de contexto 1 (v1) no coincide con el actual en DB (v2)");
    }

    /// <summary>
    /// Test 13 — Mismo escenario para la entidad Servicio.
    /// Verifica que B-02 aplique también a Servicios.
    /// </summary>
    [Fact]
    public async Task ActualizarServicio_VersionObsoleta_LanzaConcurrencyException()
    {
        // Arrange
        var sucursal  = await SeedHelper.SeedSucursalAsync(Db);
        var servicio  = await SeedHelper.SeedServicioAsync(Db, sucursal.Id, activo: true);

        // Contexto 1 lee el servicio (RowVersion = v1)
        var db1         = Fixture.CreateDbContext();
        var servicioC1  = await db1.Servicios.FindAsync(servicio.Id);
        servicioC1.Should().NotBeNull();

        // Contexto 2 actualiza primero (RowVersion = v2)
        var db2         = Fixture.CreateDbContext();
        var servicioC2  = await db2.Servicios.FindAsync(servicio.Id);
        servicioC2!.Nombre = "Nombre desde contexto 2";
        await db2.SaveChangesAsync();

        // Contexto 1 intenta actualizar con RowVersion obsoleto
        servicioC1!.Nombre = "Nombre desde contexto 1";
        var act = async () => await db1.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "el RowVersion del contexto 1 ya fue superado por el contexto 2");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<TurneroApp.Domain.Entities.Turno> SeedTurnoAsync()
    {
        var sucursal = await SeedHelper.SeedSucursalAsync(Db);
        var cliente  = await SeedHelper.SeedClienteAsync(Db, sucursal.Id);
        var servicio = await SeedHelper.SeedServicioAsync(Db, sucursal.Id, activo: true);
        var prof     = await SeedHelper.SeedProfesionalAsync(Db, sucursal.Id);
        return await SeedHelper.SeedTurnoAsync(Db, sucursal.Id, cliente.Id,
            servicio.Id, prof.UsuarioId);
    }
}
```

---

## Consideraciones de implementación

### `xid` vs `bytea` en Postgres

El tipo `xid` de Postgres es el identificador de transacción nativo — cambia automáticamente
en cada `UPDATE` sin necesidad de triggers. Es la opción más eficiente para RowVersion en Postgres.

Si la entidad usa `byte[]` con `[Timestamp]`, el comportamiento es idéntico; solo cambia el
tipo de columna (`bytea` vs `xid`).

### Manejo de `DbUpdateConcurrencyException` en el controller

Para que el usuario reciba un 409 (y no un 500), el controller debe capturar la excepción:

```csharp
catch (DbUpdateConcurrencyException)
{
    return Conflict("El recurso fue modificado por otro usuario. Recarga e intenta de nuevo.");
}
```

### Por qué test a nivel de servicio y no HTTP para B-02

A nivel HTTP, para forzar la condición de carrera necesitaríamos dos requests que:
1. Lean el mismo recurso.
2. El segundo finalice su UPDATE antes que el primero.

Esto es posible pero frágil (depende del timing). A nivel de `DbContext`, podemos controlar
exactamente el orden de las operaciones, haciendo el test determinístico y rápido.
