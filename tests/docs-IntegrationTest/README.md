# TurneroApp.IntegrationTests

Tests de integración que verifican lógica de negocio, aislamiento multi-tenant y seguridad contra una base de datos PostgreSQL real.
A diferencia de los tests unitarios (que mockean repositorios), estos tests usan repositorios reales y validan que las queries EF Core funcionan correctamente.

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

# Filtrar por suite
dotnet test tests/TurneroApp.IntegrationTests --filter "Security"
dotnet test tests/TurneroApp.IntegrationTests --filter "CreateAsync"
```

Testcontainers levanta un contenedor `postgres:16-alpine` automáticamente, aplica las migrations reales, ejecuta los tests y destruye el contenedor al finalizar.

---

## Suites de tests

### Tests de servicio — `PostgresFixture`

| Archivo | Tests | Documenta |
|---|---|---|
| `TurnoServiceIntegrationTests.cs` | 3 | Creación de turnos: límite de plan, conflicto de horario, happy path |
| `SlotsDisponiblesIntegrationTests.cs` | 6 | Algoritmo de slots: franjas, conflictos, duración, buffer, horario laboral |
| `MultiTenantAislamientoIntegrationTests.cs` | 5 | Aislamiento de datos entre sucursales |
| `ConcurrenciaOptimistaTests.cs` | 2 | RowVersion B-02: dos contextos editando el mismo registro simultáneamente |
| `QueryFiltersTests.cs` | 5 | Global Query Filters M-04/M-05: activo/inactivo en GetAllAsync y GetByIdAsync |

### Tests HTTP — `HttpTestFixture` (pipeline completo)

| Archivo | Tests | Hallazgo | Documenta |
|---|---|---|---|
| `AutorizacionHttpTests.cs` | 8 | C-02, A-01, A-06 | 401/403 en endpoints protegidos, campo Origen server-side |
| `IdorHttpTests.cs` | 2 | A-02 | IDOR entre sucursales, bypass SuperAdmin |
| `RateLimitingTests.cs` | 2 | M-06 | Rate limiting en /api/auth/login |
| `ConflictoHorarioConcurrenteTests.cs` | 1 | M-03 | Race condition TOCTOU en creación de turnos |

**Total: 34 tests de integración**

**Documentación detallada por suite:**
- [turno-service.md](turno-service.md) — `TurnoServiceIntegrationTests`
- [slots-disponibles.md](slots-disponibles.md) — `SlotsDisponiblesIntegrationTests`
- [multi-tenant-aislamiento.md](multi-tenant-aislamiento.md) — `MultiTenantAislamientoIntegrationTests`
- [concurrencia-optimista.md](concurrencia-optimista.md) — `ConcurrenciaOptimistaTests`
- [query-filters.md](query-filters.md) — `QueryFiltersTests`
- [security-http.md](security-http.md) — Tests HTTP de seguridad (AutorizacionHttpTests, IdorHttpTests, RateLimitingTests, ConflictoHorarioConcurrenteTests)

---

## Estructura de archivos

```
tests/TurneroApp.IntegrationTests/
  TurneroApp.IntegrationTests.csproj
  Infrastructure/
    PostgresFixture.cs               ← Testcontainers + migrations + Respawn (service tests)
    IntegrationTestBase.cs           ← base para service tests: reset DB + DbContext fresco
    HttpTestFixture.cs               ← WebApplicationFactory<Program> + Testcontainers (HTTP tests)
    HttpIntegrationTestBase.cs       ← base para HTTP tests: reset DB + HttpClient fresco
    TestConstants.cs                 ← JWT secret y credenciales SA de test
    TokenHelper.cs                   ← generación de JWT firmados para tests HTTP
  Helpers/
    SeedHelper.cs                    ← seed de entidades (respeta orden FK)
    ServiceFactory.cs                ← construye servicios con repos reales
  Services/
    TurnoServiceIntegrationTests.cs          ← 3 tests
    SlotsDisponiblesIntegrationTests.cs      ← 6 tests
    MultiTenantAislamientoIntegrationTests.cs ← 5 tests
    ConcurrenciaOptimistaTests.cs            ← 2 tests (B-02)
    QueryFiltersTests.cs                     ← 5 tests (M-04, M-05)
  Security/
    AutorizacionHttpTests.cs                 ← 8 tests HTTP (C-02, A-01, A-06)
    IdorHttpTests.cs                         ← 2 tests HTTP (A-02)
    RateLimitingTests.cs                     ← 2 tests HTTP (M-06)
    ConflictoHorarioConcurrenteTests.cs      ← 1 test HTTP (M-03)
```

---

## Dos infraestructuras de tests

El proyecto tiene dos infraestructuras separadas según el tipo de test:

### Infraestructura de servicio — `PostgresFixture` + `IntegrationTestBase`

Para tests que invocan servicios de aplicación directamente (sin HTTP).

```
[Suite de tests]
      │
      ▼
PostgresFixture.InitializeAsync()
  ├── SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true)
  ├── StartAsync()          ← Docker pull + container start
  ├── MigrateAsync()        ← migrations reales, con NullTenantContext
  └── Respawner.CreateAsync ← snapshot del estado inicial

      │  (compartido entre todos los tests de la clase)
      ▼

[Test N] → ResetAsync() → CreateDbContext() → ServiceFactory.Build*() → verifica
```

**`IntegrationTestBase`** expone:
- `Db` — `AppDbContext` con `NullTenantContext` (query filters desactivados)
- `Fixture` — acceso al `PostgresFixture` compartido

### Infraestructura HTTP — `HttpTestFixture` + `HttpIntegrationTestBase`

Para tests que envían requests HTTP reales al pipeline completo de ASP.NET Core.

```
[Suite de tests — colección "HttpTests"]
      │
      ▼
HttpTestFixture.InitializeAsync()
  ├── StartAsync()          ← Docker pull + container start
  ├── MigrateAsync()        ← migrations vía DI scope
  └── Respawner.CreateAsync

      │  (compartido entre todos los tests de la colección "HttpTests")
      ▼

[Test N] → ResetDbAsync() → CreateClient() → CreateDbContext() → HTTP request → verifica
```

**`HttpIntegrationTestBase`** expone:
- `Client` — `HttpClient` configurado contra el TestServer
- `Db` — `AppDbContext` directo (seed y asserts fuera del pipeline)
- `Factory` — acceso al `HttpTestFixture` para crear clientes adicionales
- `UsarToken(token)` / `SinToken()` — helpers de autenticación

**`TokenHelper`** genera JWTs firmados con `TestConstants.JwtSecret` para simular:
- `TokenHelper.GenerarToken()` — usuario normal
- `TokenHelper.ParaSucursal(sucursalId)` — usuario de una sucursal específica
- `TokenHelper.SuperAdmin()` — super admin

**`RateLimitTestFixture`** hereda de `HttpTestFixture` y preserva el rate limiter de producción para los tests de M-06.

---

## Helpers

### `SeedHelper`

Métodos estáticos para insertar entidades en la DB antes de cada test.

| Método | Entidad | Parámetros configurables |
|---|---|---|
| `SeedPlanAsync` | `Plan` | `maxTurnosMes`, `maxUsuarios`, `maxServicios` |
| `SeedSucursalAsync` | `Sucursal` | — |
| `SeedSuscripcionAsync` | `SucursalSuscripcion` | `estado`, `diasVencimiento` |
| `SeedClienteAsync` | `Cliente` | `activo` (bool, default true) |
| `SeedServicioAsync` | `Servicio` | `duracionAgendaMin`, `activo` (bool) |
| `SeedProfesionalAsync` | `Usuario` | — |
| `SeedProfesionalConUSRAsync` | `Usuario` + `UsuarioSucursalRol` | `sucursalId` |
| `SeedTurnoAsync` | `Turno` | `fechaHoraInicio`, `duracionMin` |
| `SeedConfiguracionAsync` | `ConfiguracionSucursal` | `horariosJson`, `buffer`, `anticipacionMin` |
| `SeedUsuarioSucursalRolAsync` | `UsuarioSucursalRol` | `rol`, `estado` |
| `SeedHorarioLaboralAsync` | `HorarioLaboral` | `dia`, `desde`, `hasta` |
| `HorarioJson` *(helper)* | `string` JSON | `diaNombre`, `apertura`, `cierre` |

> **Nota:** Los tests HTTP que crean turnos usan `SeedProfesionalConUSRAsync` (inserta `Usuario` + `UsuarioSucursalRol`) porque `TurnoService` requiere que el profesional sea miembro activo de la sucursal. `SeedProfesionalAsync` solo inserta el `Usuario`.

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

### `ServiceFactory`

Construye servicios con **repositorios reales** y **side-effects mockeados**.

**`BuildTurnoService(db)`** — repos reales: `TurnoRepository, ServicioRepository, ClienteRepository, SucursalRepository, ComentarioTurnoRepository, SuscripcionRepository`. Side-effects mockeados: email, notificaciones.

**`BuildPublicoService(db)`** — todos los repos reales, sin mocks (el servicio no tiene side-effects externos).

---

## Dependencias NuGet

| Paquete | Versión | Uso |
|---|---|---|
| `Testcontainers.PostgreSql` | 3.10.0 | Levanta contenedor PostgreSQL en tests |
| `Respawn` | 6.2.1 | Trunca tablas entre tests (reset rápido) |
| `Microsoft.AspNetCore.Mvc.Testing` | 8.0.0 | WebApplicationFactory para tests HTTP |
| `Microsoft.IdentityModel.Tokens` | 8.0.2 | Generación de JWT en TokenHelper |
| `System.IdentityModel.Tokens.Jwt` | 8.0.2 | Generación de JWT en TokenHelper |
| `xunit` | 2.6.2 | Framework de tests |
| `FluentAssertions` | 6.12.0 | Assertions legibles |
| `Moq` | 4.20.70 | Mock de side-effects (email, notificaciones) |
| `Microsoft.NET.Test.Sdk` | 17.8.0 | Runner de tests |

---

## Problemas conocidos y sus fixes

### Migration duplicada — `column already exists`

**Síntoma:** `Npgsql.PostgresException : 42701: column "MensajeWhatsApp24h" already exists` al correr `MigrateAsync()`.

**Causa:** `20260316150855_AgregarPlantillasWhatsApp` y `20260318152628_AddMercadoPagoFields` eran copias exactas. En la DB local no falla (EF saltea las ya aplicadas). En el contenedor limpio ambas se ejecutan en orden.

**Fix:** El `Up()` de `AddMercadoPagoFields` se dejó vacío (no-op).

---

### `NullReferenceException` en query filter multi-tenant

**Síntoma:** `System.NullReferenceException` en `lambda_method(Closure, QueryContext)` dentro de los repositorios.

**Causa:** `AppDbContext` creado sin `ITenantContext`. EF Core 8 compila los query filters a delegados IL que acceden a `_tenantContext.SucursalId` aunque sea `null`.

**Fix:** `PostgresFixture.CreateDbContext()` y `HttpTestFixture.CreateDbContext()` pasan un `NullTenantContext` (stub interno con `SucursalId = null`). Esto hace que el filtro evalúe `!SucursalId.HasValue → true` y deje pasar todos los registros.

---

### `Cannot write DateTime with Kind=UTC to timestamp without time zone`

**Síntoma:** `System.ArgumentException` al hacer `SaveChangesAsync` en `SeedTurnoAsync`.

**Causa:** Las columnas de fecha de `turnos` son `timestamp without time zone`. Npgsql 8 rechaza `DateTime` con `Kind=Utc` en esas columnas.

**Fix:** En ambos fixtures se activa `AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true)` antes de cualquier operación con la DB.

---

### `public partial class Program` requerido

**Síntoma:** `xUnit1000: Test classes must be public` + `CS0060: accessibility mismatch`.

**Causa:** Las top-level statements en `Program.cs` generan una clase `internal Program`. `WebApplicationFactory<Program>` no puede usarse en clases `public` si `Program` es `internal`.

**Fix:** Al final de `Program.cs` se agregó `public partial class Program { }`, que declara explícitamente `Program` como `public`.
