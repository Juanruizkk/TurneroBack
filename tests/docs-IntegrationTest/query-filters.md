# Tests de Global Query Filters (M-04 / M-05)

Tests de integración que verifican el comportamiento de los Global Query Filters de EF Core para el campo `Activo` / `Activa`, y que `GetByIdAsync` los respeta correctamente tras el fix de M-05.

**Proyecto:** `TurneroApp.IntegrationTests`
**Archivo:** `Services/QueryFiltersTests.cs`
**Infraestructura:** `PostgresFixture` + `IntegrationTestBase`
**Hallazgos:** M-04, M-05

---

## Contexto

### M-04 — Global Query Filters en `HasQueryFilter`

`AppDbContext` configura filtros globales sobre `Servicio` y `Cliente`:

```csharp
entity.HasQueryFilter(e => e.Activo == true);
```

Estos filtros se aplican automáticamente a **todas** las queries EF Core (salvo `IgnoreQueryFilters()`). Verifican que los registros inactivos (borrado lógico) sean invisibles para las operaciones normales.

### M-05 — `FindAsync` bypaseaba los Global Query Filters

El `GenericRepository<T>.GetByIdAsync` usaba `FindAsync(id)`. `FindAsync` primero busca en el cache del contexto y luego en la DB, pero **siempre bypasea** los Global Query Filters. Si un servicio inactivo estaba en cache o se buscaba directamente por ID, era devuelto como si estuviera activo.

**Fix M-05:** `GetByIdAsync` ahora usa `FirstOrDefaultAsync(e => EF.Property<Guid>(e, "Id") == id)`, que respeta los Global Query Filters.

---

## Tests

### Test 14 — `GetAllAsync` no incluye servicio inactivo

```
GetAllAsync_ServicioInactivo_NoAparece
```

**Arrange:** Seed un servicio activo y uno inactivo en la misma sucursal.

**Act:** `new ServicioRepository(Db).GetAllAsync()`

**Assert:**
- El servicio activo **aparece** en el resultado
- El servicio inactivo **no aparece** (filtrado por `HasQueryFilter`)

---

### Test 14b — `GetAllAsync` no incluye cliente inactivo

```
GetAllAsync_ClienteInactivo_NoAparece
```

Mismo patrón que Test 14 pero con `ClienteRepository`. Verifica que M-04 aplique a `Cliente` además de `Servicio`.

**Assert:**
- Cliente activo → aparece
- Cliente inactivo → no aparece

---

### Test 15 — `GetByIdAsync` de servicio inactivo devuelve `null`

```
GetByIdAsync_ServicioInactivo_DevuelveNull
```

**Arrange:** Seed un servicio con `Activo = false`.

**Act:** `new ServicioRepository(Db).GetByIdAsync(inactivo.Id)`

**Assert:** `result == null`

> Con el código previo al fix (usando `FindAsync`), este test **hubiera fallado** — `FindAsync` devolvería la entidad inactiva ignorando el query filter. El test documenta y protege el comportamiento correcto post-fix.

---

### Test 16 — Regresión: `GetByIdAsync` de servicio activo lo encuentra

```
GetByIdAsync_ServicioActivo_LoEncuentra
```

Verifica que el fix de M-05 no rompió el caso normal.

**Assert:**
- `result != null`
- `result.Id == activo.Id`

---

### Test 16b — `GetByIdAsync` de cliente inactivo devuelve `null`

```
GetByIdAsync_ClienteInactivo_DevuelveNull
```

Mismo patrón que Test 15 pero con `ClienteRepository`. Verifica cobertura completa del fix de M-05 en `Cliente`.

---

## Resumen de cobertura

| Test | Entidad | Método | Resultado esperado |
|---|---|---|---|
| 14 | `Servicio` | `GetAllAsync` | Inactivo excluido del listado |
| 14b | `Cliente` | `GetAllAsync` | Inactivo excluido del listado |
| 15 | `Servicio` | `GetByIdAsync` | Inactivo → `null` (respeta query filter) |
| 16 | `Servicio` | `GetByIdAsync` | Activo → entidad encontrada (regresión) |
| 16b | `Cliente` | `GetByIdAsync` | Inactivo → `null` (respeta query filter) |

---

## Por qué tests de integración y no unitarios

Los Global Query Filters de EF Core son configurados en `AppDbContext` y se expresan como LINQ-to-SQL. Solo se verifican correctamente contra una base de datos real — no hay forma de probar que el filtro SQL generado es correcto usando un repositorio en memoria. Un test unitario con mocks no detectaría si `FindAsync` bypasea el filtro.
