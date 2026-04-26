# Tests de Concurrencia Optimista — RowVersion (B-02)

Tests de integración que verifican el mecanismo de concurrencia optimista con `RowVersion` (`byte[]`) configurado como `IsRowVersion()` en EF Core.

**Proyecto:** `TurneroApp.IntegrationTests`
**Archivo:** `Services/ConcurrenciaOptimistaTests.cs`
**Infraestructura:** `PostgresFixture` + `IntegrationTestBase`
**Hallazgo:** B-02

---

## Qué verifica

Que EF Core lance `DbUpdateConcurrencyException` cuando dos contextos independientes intentan modificar el mismo registro y el segundo guarda después de que el primero ya incrementó el `RowVersion`.

---

## Cómo funciona `RowVersion` en EF Core + PostgreSQL

La entidad tiene un campo `byte[] RowVersion` configurado como `IsRowVersion()`. PostgreSQL lo implementa internamente como columna `xmin` (transaction ID). EF Core incluye automáticamente el `RowVersion` leído en el `WHERE` de los `UPDATE`:

```sql
UPDATE turnos
SET observaciones = @obs
WHERE id = @id AND "RowVersion" = @rowversion_leido
```

Si entre la lectura y la escritura otro contexto modificó la fila, el `RowVersion` en DB ya cambió. El `UPDATE` afecta 0 filas y EF Core lanza `DbUpdateConcurrencyException`.

---

## Test 12 — Turno: versión obsoleta lanza `DbUpdateConcurrencyException`

```
ActualizarTurno_VersionObsoleta_LanzaConcurrencyException
```

**Arrange:**
- Seed completo: sucursal → cliente → servicio → profesional → turno
- `db1` lee el turno (captura `RowVersion = v1`)
- `db2` también lo lee, modifica `Observaciones` y llama `SaveChangesAsync()` → `RowVersion` pasa a `v2`

**Act:** `db1` intenta `SaveChangesAsync()` con `RowVersion = v1` (ya obsoleto)

**Assert:** `ThrowAsync<DbUpdateConcurrencyException>`

Los dos `AppDbContext` son creados con `Fixture.CreateDbContext()` — completamente independientes, sin change tracking compartido. Este patrón replica exactamente lo que ocurre en producción cuando dos usuarios HTTP editan el mismo turno.

---

## Test 13 — Servicio: mismo escenario para la entidad `Servicio`

```
ActualizarServicio_VersionObsoleta_LanzaConcurrencyException
```

Mismo patrón que Test 12 pero con la entidad `Servicio` y el campo `Nombre`. Verifica que B-02 aplica a `Servicio` además de `Turno`.

**Arrange:**
- Seed: sucursal → servicio activo
- `db1` lee el servicio (`RowVersion = v1`)
- `db2` lo lee, cambia `Nombre` y guarda → `v2`

**Act:** `db1` intenta guardar con `RowVersion = v1`

**Assert:** `ThrowAsync<DbUpdateConcurrencyException>`

---

## Detalles de implementación del test

### Por qué `FirstOrDefaultAsync` y no `FindAsync`

`FindAsync` puede devolver la entidad desde el cache del contexto sin emitir SQL. Si el contexto ya tiene la entidad en tracking, no releeería el `RowVersion` actualizado. `FirstOrDefaultAsync` siempre emite un `SELECT` real y obtiene el `RowVersion` correcto de la DB.

### Cleanup explícito

Los dos contextos adicionales (`db1`, `db2`) son `await`-disposed al final del test. La clase hereda `IntegrationTestBase` que maneja `Db` (el contexto principal), pero los contextos creados manualmente en el test deben limpiarse explícitamente para no saturar las conexiones del pool durante la ejecución paralela.

---

## Reglas de negocio cubiertas

| Regla | Test |
|---|---|
| Dos ediciones simultáneas de un `Turno` → la segunda falla | Test 12 |
| Dos ediciones simultáneas de un `Servicio` → la segunda falla | Test 13 |
| `RowVersion` actúa como "lock optimista" sin bloquear reads | Ambos — los dos `FindAsync` concurrentes no se bloquean entre sí |
