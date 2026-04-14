# TurneroApp.IntegrationTests

Tests de integración que verifican lógica de negocio y aislamiento multi-tenant contra una base de datos PostgreSQL real.
A diferencia de los tests unitarios (que mockean repositorios), estos tests usan repositorios reales y validan que las queries EF Core funcionan correctamente en la DB.

---

## Pre-requisitos

- **Docker** corriendo localmente (Testcontainers lo usa para levantar el contenedor Postgres)
- .NET 8 SDK

---

## Cómo correr

```bash
cd backend

# Solo integración
dotnet test tests/TurneroApp.IntegrationTests -v normal

# Todos los tests (unitarios + integración)
dotnet test TurneroApp.sln -v normal

# Filtrar por nombre
dotnet test tests/TurneroApp.IntegrationTests --filter "CreateAsync"
```

Testcontainers levanta un contenedor `postgres:16-alpine` automáticamente, aplica las migrations reales, ejecuta los tests y destruye el contenedor al finalizar.

---

## Suites de tests

| Archivo | Tests | Documenta |
|---|---|---|
| `TurnoServiceIntegrationTests.cs` | 3 | Creación de turnos: límite de plan, conflicto de horario, happy path |
| `SlotsDisponiblesIntegrationTests.cs` | 6 | Algoritmo de slots: franjas, conflictos, duración, buffer, horario laboral |
| `MultiTenantAislamientoIntegrationTests.cs` | 5 | Aislamiento de datos entre sucursales |

**Documentación detallada por suite:**
- [turno-service.md](turno-service.md) — `TurnoServiceIntegrationTests`
- [slots-disponibles.md](slots-disponibles.md) — `SlotsDisponiblesIntegrationTests`
- [multi-tenant-aislamiento.md](multi-tenant-aislamiento.md) — `MultiTenantAislamientoIntegrationTests`

---

## Estructura de archivos

```
tests/TurneroApp.IntegrationTests/
  TurneroApp.IntegrationTests.csproj
  Infrastructure/
    PostgresFixture.cs               ← Testcontainers + migrations + Respawn
    IntegrationTestBase.cs           ← clase base: reset DB + DbContext fresco por test
  Helpers/
    SeedHelper.cs                    ← seed de entidades (respeta orden FK)
    ServiceFactory.cs                ← construye servicios con repos reales
  Services/
    TurnoServiceIntegrationTests.cs          ← 3 tests
    SlotsDisponiblesIntegrationTests.cs      ← 6 tests
    MultiTenantAislamientoIntegrationTests.cs ← 5 tests
```

---

## Arquitectura de la infraestructura de tests

### `PostgresFixture` — Ciclo de vida del contenedor

`PostgresFixture` implementa `IAsyncLifetime` y es compartido entre todos los tests de una clase vía `IClassFixture<PostgresFixture>`.

**Flujo de `InitializeAsync`:**
1. Activa `Npgsql.EnableLegacyTimestampBehavior` (ver [Problemas conocidos](#problemas-conocidos-y-sus-fixes))
2. Levanta el contenedor `postgres:16-alpine` con Testcontainers
3. Aplica todas las EF Core migrations con `db.Database.MigrateAsync()` usando un `NullTenantContext`
4. Abre una conexión `NpgsqlConnection` persistente
5. Inicializa **Respawn** apuntando al schema `public`

**Flujo de `DisposeAsync`:**
1. Cierra la conexión Npgsql
2. Destruye el contenedor Docker

```
[Suite de tests]
      │
      ▼
PostgresFixture.InitializeAsync()
  ├── SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true)
  ├── StartAsync()          ← Docker pull + container start
  ├── MigrateAsync()        ← migrations reales, con NullTenantContext
  └── Respawner.CreateAsync ← snapshot del estado inicial

      │  (se comparte entre todos los tests de la clase)
      ▼

[Test 1] → ResetAsync() → CreateDbContext() → ejecuta → verifica
[Test 2] → ResetAsync() → CreateDbContext() → ejecuta → verifica
[Test 3] → ResetAsync() → CreateDbContext() → ejecuta → verifica

      │
      ▼
PostgresFixture.DisposeAsync()
  ├── connection.DisposeAsync()
  └── container.DisposeAsync()   ← contenedor destruido
```

### `IntegrationTestBase` — Aislamiento entre tests

Cada test hereda de `IntegrationTestBase`, que en `InitializeAsync`:
1. Llama a `Fixture.ResetAsync()` — Respawn trunca todas las tablas (excepto `__EFMigrationsHistory`)
2. Llama a `Fixture.CreateDbContext()` — crea un `AppDbContext` nuevo con `NullTenantContext`

Esto garantiza que cada test arranca con la DB vacía y su propio contexto, replicando el comportamiento de producción (cada request HTTP tiene su propio `DbContext`).

> El `NullTenantContext` (con `SucursalId = null`) desactiva los global query filters multi-tenant. Sin él, EF Core lanza `NullReferenceException` al evaluar `_tenantContext.SucursalId.HasValue` durante la compilación del árbol de expresiones LINQ.

---

## Helpers

### `SeedHelper`

Métodos estáticos para insertar entidades en la DB antes de cada test. Cada método hace `SaveChangesAsync` individualmente para respetar el orden de FK constraints.

| Método | Entidad creada | Parámetros configurables |
|---|---|---|
| `SeedPlanAsync` | `Plan` | `maxTurnosMes`, `maxUsuarios`, `maxServicios` |
| `SeedSucursalAsync` | `Sucursal` | — |
| `SeedSuscripcionAsync` | `SucursalSuscripcion` | `estado`, `diasVencimiento` |
| `SeedClienteAsync` | `Cliente` | — |
| `SeedServicioAsync` | `Servicio` | `duracionAgendaMin` |
| `SeedProfesionalAsync` | `Usuario` | — |
| `SeedTurnoAsync` | `Turno` | `fechaHoraInicio`, `duracionMin` |
| `SeedConfiguracionAsync` | `ConfiguracionSucursal` | `horariosJson`, `buffer`, `anticipacionMin` |
| `SeedUsuarioSucursalRolAsync` | `UsuarioSucursalRol` | `rol`, `estado` |
| `SeedHorarioLaboralAsync` | `HorarioLaboral` | `dia`, `desde`, `hasta` |
| `HorarioJson` *(helper)* | `string` JSON | `diaNombre`, `apertura`, `cierre` |

**Orden de seed completo (requerido por FK constraints):**
```
Plan → Sucursal → SucursalSuscripcion
                → ConfiguracionSucursal
                → Cliente
                → Servicio
Usuario (profesional)
  └── UsuarioSucursalRol (vincula Usuario ↔ Sucursal)
        └── HorarioLaboral
→ Turno (referencia Sucursal + Cliente + Servicio + Usuario)
```

> **Nota:** `SeedProfesionalAsync` inserta un `Usuario` real porque `Turno.ProfesionalId` tiene FK con `OnDelete(Restrict)` hacia `usuarios.Id`. `UsuarioSucursalRol` es necesario para que `PublicoService` reconozca al usuario como profesional activo de la sucursal.

### `ServiceFactory`

Construye servicios con **repositorios reales** (backed por la DB del contenedor) y **side-effects mockeados** (email y notificaciones).

**`BuildTurnoService(db)`**
```csharp
// Repos reales
TurnoRepository, ServicioRepository, ClienteRepository,
SucursalRepository, ComentarioTurnoRepository, SuscripcionRepository
// Side-effects mockeados
Mock.Of<IEmailService>(), Mock.Of<ITurnoNotificacionService>()
```
`horarioRepo` y `usuarioServicioRepo` se omiten intencionalmente: los tests de `TurnoService` no cubren validación de horario laboral ni servicios asignados.

**`BuildPublicoService(db)`**
```csharp
// Todos los repos reales (sin mocks):
SucursalRepository, ServicioRepository, TurnoRepository,
UsuarioSucursalRolRepository, HorarioLaboralRepository, UsuarioServicioRepository
```
`PublicoService` no tiene side-effects externos (no envía emails ni notificaciones), por eso no requiere mocks.

---

## Tests

### Clase: `TurnoServiceIntegrationTests`

**Servicio bajo test:** `TurnoService.CreateAsync`

---

#### Test 1 — Límite mensual del plan

```
CreateAsync_CuandoLimiteMensualAlcanzado_LanzaInvalidOperationException
```

**Escenario:** El plan tiene `MaxTurnosMes = 2` y ya existen 2 turnos en el mes actual. Se intenta crear un 3er turno.

**Arrange:**
- Plan con `maxTurnosMes: 2`
- Suscripción activa (PruebActiva, vence en 30 días)
- 2 turnos ya persistidos en la DB para el mes en curso

**Assert:**
- Lanza `InvalidOperationException` con mensaje `*Actualizá tu suscripción*`
- La DB sigue teniendo exactamente 2 turnos (el 3er no se persistió)

---

#### Test 2 — Conflicto de horario

```
CreateAsync_CuandoConflictoHorario_LanzaInvalidOperationException
```

**Escenario:** Existe un turno de 10:00 a 10:30. Se intenta crear otro turno a las 10:15 (que solapa el existente).

**Arrange:**
- Plan sin límite de turnos
- Turno existente: `10:00 – 10:30` (30 min)
- Nuevo turno solicitado: `10:15` (se superpondría)

**Assert:**
- Lanza `InvalidOperationException` con mensaje `*ya tiene un turno en el horario seleccionado*`
- La DB sigue teniendo exactamente 1 turno

---

#### Test 3 — Happy path / Persistencia real

```
CreateAsync_CuandoDatosValidos_PersisteTurnoConCamposCorrectos
```

**Escenario:** Todos los datos son válidos. Se verifica que el turno se persiste correctamente en PostgreSQL.

**Arrange:**
- Plan sin límite, suscripción activa
- Servicio de 45 minutos de duración
- Inicio: `2026-07-20 09:00 UTC`

**Assert sobre el DTO de respuesta:**
- `result` no es null
- `result.ClienteNombre` no está vacío

**Assert sobre la DB (persistencia real):**
- `ClienteId`, `ServicioId`, `ProfesionalId` coinciden con los seeds
- `FechaHoraFin = 09:45 UTC` (inicio + 45 min calculado por el servicio)
- `TokenGestion` no está vacío

---

## Dependencias NuGet

| Paquete | Versión | Uso |
|---|---|---|
| `Testcontainers.PostgreSql` | 3.10.0 | Levanta contenedor PostgreSQL en tests |
| `Respawn` | 6.2.1 | Trunca tablas entre tests (reset rápido) |
| `xunit` | 2.6.2 | Framework de tests |
| `FluentAssertions` | 6.12.0 | Assertions legibles |
| `Moq` | 4.20.70 | Mock de side-effects (email, notificaciones) |
| `Microsoft.NET.Test.Sdk` | 17.8.0 | Runner de tests |

---

## Decisiones de diseño

| Decisión | Motivo |
|---|---|
| Testcontainers en lugar de SQLite | Las queries usan tipos y comportamientos específicos de PostgreSQL (ej: overlapping de rangos de fechas). SQLite no garantiza el mismo comportamiento. |
| Respawn en lugar de recrear la DB | Recrear el schema completo por test sería muy lento. Respawn trunca las tablas en milisegundos reutilizando el mismo contenedor. |
| Un `AppDbContext` por test con `NullTenantContext` | Replica el ciclo de vida de producción y evita `NullReferenceException` en los global query filters multi-tenant. |
| `horarioRepo` y `usuarioServicioRepo` omitidos | Los 3 tests se focalizan en límite de plan, conflicto de horario y persistencia. Incluir validaciones de horario laboral requeriría seed adicional complejo sin agregar valor a estos escenarios. |
| Side-effects (email, notificaciones) mockeados | Son efectos externos irrelevantes para verificar la lógica de negocio y persistencia de datos. |
| `EnableLegacyTimestampBehavior = true` | Las columnas `FechaHoraInicio`, `FechaHoraFin` y `FechaCreacion` de `turnos` son `timestamp without time zone`. Npgsql 8 rechaza `DateTimeKind.Utc` en esas columnas; el switch legacy permite escribirlas sin cambiar las entidades ni las migrations. |

---

## Problemas conocidos y sus fixes

### Migration duplicada — `column already exists`

**Síntoma:** `Npgsql.PostgresException : 42701: column "MensajeWhatsApp24h" of relation "configuraciones_sucursal" already exists` al correr `MigrateAsync()`.

**Causa:** `20260316150855_AgregarPlantillasWhatsApp` y `20260318152628_AddMercadoPagoFields` eran copias exactas: ambas intentaban agregar las columnas `MensajeWhatsApp24h` y `MensajeWhatsApp3h`. En la DB local nunca falló porque EF consulta `__EFMigrationsHistory` y saltea las ya aplicadas. En el contenedor limpio de Testcontainers ambas se ejecutan en orden y la segunda falla.

**Fix aplicado:** El `Up()` de `AddMercadoPagoFields` se dejó vacío (no-op). No se eliminó el archivo porque ya está registrado en `__EFMigrationsHistory` de la DB de producción — borrarlo rompería el historial allí.

---

### `NullReferenceException` en query filter multi-tenant

**Síntoma:** `System.NullReferenceException` en `lambda_method(Closure, QueryContext)` dentro de `SuscripcionRepository.GetBySucursalAsync`.

**Causa:** `AppDbContext` fue creado sin pasar un `ITenantContext`. El global query filter de `SucursalSuscripcion` contiene `_tenantContext == null || !_tenantContext.SucursalId.HasValue || ...`. EF Core 8 compila el filtro a un delegado IL; cuando ejecuta el delegado, intenta acceder a `_tenantContext.SucursalId` aunque `_tenantContext` sea `null`, lo que provoca el `NullReferenceException`.

**Fix aplicado:** `PostgresFixture.CreateDbContext()` ahora pasa un `NullTenantContext` (stub interno con `SucursalId = null`). Esto hace que el filtro evalúe `!_tenantContext.SucursalId.HasValue → true` y deje pasar todos los registros sin filtrar por sucursal.

---

### `Cannot write DateTime with Kind=UTC to timestamp without time zone`

**Síntoma:** `System.ArgumentException` al hacer `SaveChangesAsync` en `SeedTurnoAsync`.

**Causa:** Las columnas `FechaHoraInicio`, `FechaHoraFin` y `FechaCreacion` de la tabla `turnos` están configuradas como `timestamp without time zone` (sin zona horaria). Npgsql 8 rechaza escribir `DateTime` con `Kind=Utc` en esas columnas, ya que considera que sería una pérdida silenciosa de información de zona.

**Fix aplicado:** En `PostgresFixture.InitializeAsync()` se activa el switch `AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true)` antes de cualquier operación con la DB. Esto restaura el comportamiento de Npgsql 6 donde `DateTimeKind` se ignoraba al escribir en columnas `timestamp`.
