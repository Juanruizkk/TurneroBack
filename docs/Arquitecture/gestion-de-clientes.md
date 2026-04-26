# Gestión de Clientes

Documento de referencia para el módulo de clientes del sistema TurneroApp.

---

## Entidad `Cliente`

Definida en `src/Domain/Entities/Cliente.cs`.

| Propiedad        | Tipo        | Descripción                                              |
|------------------|-------------|----------------------------------------------------------|
| `Id`             | `Guid`      | Identificador único, asignado manualmente                |
| `SucursalId`     | `Guid`      | Sucursal a la que pertenece el cliente                   |
| `Nombre`         | `string`    | Nombre del cliente (max 100)                             |
| `Apellido`       | `string`    | Apellido del cliente (max 100)                           |
| `Telefono`       | `string`    | Teléfono de contacto (max 50)                            |
| `Email`          | `string?`   | Email opcional (max 200)                                 |
| `Observaciones`  | `string?`   | Notas libres del staff (max 1000)                        |
| `Activo`         | `bool`      | `true` = activo, `false` = archivado. Default: `true`    |
| `FechaArchivado` | `DateTime?` | Timestamp UTC de cuando fue archivado. `null` si activo  |

### Métodos de dominio

```csharp
cliente.Archivar();     // Activo = false, FechaArchivado = DateTime.UtcNow
cliente.Desarchivar();  // Activo = true,  FechaArchivado = null
```

### Relaciones

- `Sucursal` (N:1) — todo cliente pertenece a una única sucursal.
- `Turnos` (1:N) — historial de turnos del cliente. `OnDelete(Restrict)` previene borrado con turnos activos.

---

## Archivado (Soft Delete)

Los clientes **no se eliminan directamente** en condiciones normales. En su lugar se **archivan**, lo que los oculta de búsquedas, listados y el selector de turnos, pero preserva todo el historial.

### Global Query Filter

En `AppDbContext` el filtro automático excluye los clientes archivados de todas las queries:

```csharp
modelBuilder.Entity<Cliente>()
    .HasQueryFilter(e =>
        e.Activo &&
        (_tenantContext == null
         || !_tenantContext.SucursalId.HasValue
         || e.SucursalId == _tenantContext.SucursalId.GetValueOrDefault()));
```

**Efecto:** cualquier `_dbSet.Where(...)` filtra automáticamente `Activo = true` a menos que se use `.IgnoreQueryFilters()`.

### Cuándo usar `IgnoreQueryFilters()`

Los repositorios usan `IgnoreQueryFilters()` para acceder a clientes archivados en estos casos:

| Operación | Método |
|---|---|
| Archivar / Desarchivar | `GetByIdIgnorandoFiltrosAsync` |
| Eliminar permanentemente | `GetByIdIgnorandoFiltrosAsync` |
| Listar archivados | `GetArchivedBySucursalAsync` |
| Validar unicidad de teléfono | `TelefonoExisteEnSucursalAsync` |
| Obtener ficha | `GetFichaAsync` |

---

## Eliminación permanente

La eliminación física solo se permite si el cliente **no tiene impacto operacional**:

### Estados de turno bloqueantes

| EstadoTurno | Bloquea eliminación |
|---|---|
| `Pendiente (0)` | ✅ |
| `Confirmado (1)` | ✅ |
| `Completado (4)` | ✅ |
| `NoShow (5)` | ✅ |
| `Reprogramado (6)` | ✅ |
| `CanceladoCliente (2)` | ❌ |
| `CanceladoStaff (3)` | ❌ |
| `PendientePago (7)` | ❌ |
| `PagoRechazado (8)` | ❌ |

Además, si el cliente tiene cualquier `PagoTurno` registrado, la eliminación también se bloquea.

### Tabla de decisión

| Condición | Eliminación | Archivado |
|---|---|---|
| Tiene turnos Pendiente / Confirmado / Completado / NoShow / Reprogramado | ❌ Bloqueada | ✅ Permitido |
| Tiene `PagoTurno` (pagos registrados) | ❌ Bloqueada | ✅ Permitido |
| Solo turnos Cancelados / PendientePago / PagoRechazado sin pagos | ✅ Permitida | ✅ Permitido |
| Sin turnos | ✅ Permitida | ✅ Permitido |

Cuando se bloquea, el servicio lanza `ClienteNoSePuedeEliminarException` (`src/Application/Exceptions/`), y el controller devuelve `409 Conflict`.

---

## Purga automática de clientes fantasma

Cuando un cliente hace booking online se crea un registro de `Cliente` + `Turno` en `PendientePago`. Si abandona el checkout, `TurnoPagoExpirationService` mueve el turno a `PagoRechazado` a los 30 minutos. El slot queda libre, pero el cliente puede quedar como registro fantasma sin valor histórico.

El mismo job llama a `PurgarClientesFantasmaAsync` para eliminarlos automáticamente.

### Condiciones de elegibilidad (ambas deben cumplirse)

| Condición | Detalle |
|---|---|
| Sin turnos bloqueantes | No tiene turnos en `Pendiente`, `Confirmado`, `Completado`, `NoShow` o `Reprogramado` |
| Sin pagos | No tiene ningún `PagoTurno` registrado |

### Qué se elimina

1. Turnos no críticos del cliente: `PagoRechazado`, `PendientePago`, `CanceladoCliente`, `CanceladoStaff` (con sus comentarios y adjuntos en cascada)
2. El registro de `Cliente`

La purga usa `IgnoreQueryFilters()` en todas las queries para alcanzar clientes archivados o turnos ocultos por el filtro global.

### Método de repositorio

`IClienteRepository.PurgarClientesFantasmaAsync(IEnumerable<Guid> clienteIds)`

Itera los IDs recibidos, evalúa cada uno con las condiciones anteriores y elimina los que califican. Retorna la cantidad de clientes eliminados. El `SaveChangesAsync` se ejecuta una sola vez al final, solo si hubo eliminaciones.

> Este método es exclusivo para uso del job. La eliminación manual desde la API sigue el flujo normal (`TieneImpactoOperacionalAsync` → `EliminarTurnosNoCriticosAsync` → `Delete`).

---

## Unicidad de teléfono por sucursal

No puede haber dos clientes **activos o archivados** con el mismo teléfono dentro de la misma sucursal.

La validación ocurre en `ClienteService` antes de crear o actualizar:

```csharp
// Create
if (await _clienteRepository.TelefonoExisteEnSucursalAsync(dto.SucursalId, dto.Telefono))
    throw new InvalidOperationException("Ya existe un cliente con ese teléfono...");

// Update
if (await _clienteRepository.TelefonoExisteEnSucursalAsync(cliente.SucursalId, dto.Telefono, excludeId: id))
    throw new InvalidOperationException("Ya existe un cliente con ese teléfono...");
```

El controller traduce `InvalidOperationException` a `400 Bad Request`.

> **Nota:** La validación es a nivel de servicio. El índice de DB `IX_clientes_SucursalId_Telefono` existe pero **no es UNIQUE** porque la base de datos de desarrollo tiene duplicados históricos. Para agregar el constraint en DB: limpiar duplicados y ejecutar una migración con `.IsUnique()` en `ClienteConfiguration`.

---

## Ficha de cliente (`ClienteFichaDto`)

El endpoint `GET /api/clientes/{id}/ficha` devuelve la ficha completa del cliente con tres secciones:

### Datos básicos
Nombre, teléfono, email, observaciones, estado de archivado.

### Resumen

| Campo | Descripción |
|---|---|
| `TotalTurnos` | Cantidad total de turnos del cliente |
| `CantidadNoShow` | Turnos en estado `NoShow` |
| `CantidadCancelaciones` | Turnos `CanceladoCliente` + `CanceladoStaff` |
| `UltimoTurno` | Fecha del último turno pasado |
| `ProximoTurno` | Fecha del próximo turno futuro |
| `TotalCobrado` | Suma de `Turno.TotalCobrado` de todos los turnos |
| `EsFrecuente` | `TotalTurnos > 5` (umbral: `UMBRAL_FRECUENTE = 5`) |
| `TieneNoShow` | `true` si tiene al menos un NoShow |

### Historial y próximos turnos

- **`Historial`**: turnos con `FechaHoraInicio <= ahora`, ordenados de más reciente a más antiguo.
- **`ProximosTurnos`**: turnos con `FechaHoraInicio > ahora`, ordenados de más próximo a más lejano.

Cada entrada de historial incluye `TokenGestion` para construir links directos de gestión.

> La ficha usa `IgnoreQueryFilters()` para funcionar incluso con clientes archivados.

---

## Flags de comportamiento en `ClienteResponseDto`

El listado de clientes (`GET /api/clientes`) incluye campos calculados por cada cliente:

| Campo | Cómo se calcula | Para qué sirve |
|---|---|---|
| `EsFrecuente` | `Turnos.Count > 5` | Badge "cliente frecuente" en UI |
| `TieneNoShow` | `Turnos.Any(NoShow)` | Advertencia en selector de turno |
| `TotalTurnos` | `Turnos.Count` | Indicador visual |
| `UltimaVisita` | Último `FechaHoraInicio <= ahora` | Ordenamiento / filtro |

> `GetBySucursalAsync` hace `Include(c => c.Turnos)` para que estos cálculos funcionen.

---

## Endpoints

Base: `GET|POST|PUT|DELETE /api/clientes`

| Método | Ruta | Descripción | Response |
|---|---|---|---|
| `GET` | `/api/clientes` | Lista clientes activos. Soporta `?sucursalId=`, `?q=`, `?telefono=`, `?email=` | `200` |
| `GET` | `/api/clientes/archivados?sucursalId=` | Lista clientes archivados de una sucursal | `200` |
| `GET` | `/api/clientes/{id}` | Obtiene un cliente por Id | `200` / `404` |
| `GET` | `/api/clientes/{id}/ficha` | Ficha completa con resumen + historial + próximos turnos | `200` / `404` |
| `GET` | `/api/clientes/{id}/turno-link/{turnoId}` | Devuelve `TokenGestion` de un turno del cliente | `200` / `404` |
| `POST` | `/api/clientes` | Crea un cliente. Valida teléfono único | `201` / `400` |
| `PUT` | `/api/clientes/{id}` | Actualiza datos. Valida teléfono único | `200` / `400` / `404` |
| `POST` | `/api/clientes/{id}/archivar` | Archiva el cliente (soft delete) | `204` / `404` |
| `POST` | `/api/clientes/{id}/desarchivar` | Desarchiva el cliente | `204` / `404` |
| `DELETE` | `/api/clientes/{id}` | Elimina permanentemente. Valida impacto operacional | `204` / `404` / `409` |
| `POST` | `/api/clientes/{id}/recordatorio-whatsapp` | Stub: WhatsApp pendiente de integración | `200` |

---

## Estructura de archivos

```
Domain/
  Entities/
    Cliente.cs                      ← Entidad + Archivar()/Desarchivar()

Application/
  DTOs/Cliente/
    ClienteResponseDto.cs           ← DTO de listado (incluye flags EsFrecuente, etc.)
    ClienteFichaDto.cs              ← DTO ficha completa (Historial + ProximosTurnos)
    CreateClienteDto.cs
    UpdateClienteDto.cs
  Exceptions/
    ClienteNoSePuedeEliminarException.cs
  Interfaces/
    IClienteRepository.cs
  Services/
    ClienteService.cs

Infrastructure/
  Data/
    AppDbContext.cs                  ← Global filter con e.Activo
    Configurations/
      ClienteConfiguration.cs       ← Columnas Activo/FechaArchivado, índice teléfono
  Repositories/
    ClienteRepository.cs            ← GetFichaAsync, TieneImpactoOperacionalAsync, etc.
  Migrations/
    ..._AddClienteArchivadoYUniqueTelefono.cs

Api/
  Controllers/
    ClientesController.cs           ← Todos los endpoints
```

---

## Migración aplicada

**`AddClienteArchivadoYUniqueTelefono`**

Cambios en DB:
- Columna `Activo` (`boolean NOT NULL DEFAULT true`)
- Columna `FechaArchivado` (`timestamp with time zone NULL`)
- Índice `IX_clientes_SucursalId_Telefono` recreado (no único, ver nota arriba)
