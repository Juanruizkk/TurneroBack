# Security Test Suite — Índice y Decisiones de Diseño

Documentación del plan de tests para verificar los 28 hallazgos de seguridad resueltos en Fases 1–4.

---

## Estructura del plan

| Archivo | Contenido |
|---------|-----------|
| [01-setup-http-factory.md](01-setup-http-factory.md) | `HttpTestFixture` + `TokenHelper` + `HttpIntegrationTestBase` |
| [02-tests-autorizacion-http.md](02-tests-autorizacion-http.md) | C-02, A-01, A-02, A-06, M-06 (HTTP-level) |
| [03-tests-concurrencia.md](03-tests-concurrencia.md) | M-03 (race condition) + B-02 (RowVersion) |
| [04-tests-query-filters.md](04-tests-query-filters.md) | M-04, M-05 (Activo filtering + GenericRepository) |
| [05-tests-unit-seguridad.md](05-tests-unit-seguridad.md) | A-04, C-03/04/05, B-03, B-04, M-01, M-02 |

---

## Tabla de todos los tests planeados

| # | Test | Hallazgo | Tipo | Clase | Prioridad |
|---|------|----------|------|-------|-----------|
| 1 | SuperAdmin sin token → 401 | C-02 | HTTP | `AutorizacionHttpTests` | Alta |
| 2 | SuperAdmin con token normal → 403 | C-02 | HTTP | `AutorizacionHttpTests` | Alta |
| 3 | Endpoints de staff sin token → 401 | A-01 | HTTP | `AutorizacionHttpTests` | Alta |
| 4 | Endpoints públicos sin token → no 401 | A-01 | HTTP | `AutorizacionHttpTests` | Alta |
| 5 | IDOR: ajustes sucursal B con token A → 403 | A-02 | HTTP | `IdorHttpTests` | Alta |
| 6 | IDOR: SuperAdmin → no 403 | A-02 | HTTP | `IdorHttpTests` | Alta |
| 7 | POST turno sin token → Origen Web | A-06 | HTTP | `AutorizacionHttpTests` | Media |
| 8 | POST turno con token → Origen Administrativo | A-06 | HTTP | `AutorizacionHttpTests` | Media |
| 9 | 11 logins → 429 en el 11mo | M-06 | HTTP | `RateLimitingTests` | Alta |
| 10 | Endpoint sin rate limit → nunca 429 | M-06 | HTTP | `RateLimitingTests` | Media |
| 11 | 5 creaciones concurrentes → 1 ok, 4 conflict | M-03 | HTTP | `ConflictoHorarioConcurrenteTests` | Alta |
| 12 | RowVersion Turno: versión obsoleta → DbUpdateConcurrencyException | B-02 | Service | `ConcurrenciaOptimistaTests` | Alta |
| 13 | RowVersion Servicio: mismo escenario | B-02 | Service | `ConcurrenciaOptimistaTests` | Media |
| 14 | Servicio inactivo no aparece en GetAll | M-04 | Service | `QueryFiltersTests` | Alta |
| 15 | GetByIdAsync servicio inactivo → null | M-05 | Service | `QueryFiltersTests` | Alta |
| 16 | GetByIdAsync servicio activo → lo encuentra | M-05 | Service | `QueryFiltersTests` | Alta |
| 17 | GenerarState + VerificarState → sucursalId correcto | A-04 | Unit | `HmacOAuthTests` | Alta |
| 18 | State alterado → null | A-04 | Unit | `HmacOAuthTests` | Alta |
| 19 | State con secret distinto → null | A-04 | Unit | `HmacOAuthTests` | Media |
| 20 | CrearOwner → password BCrypt | C-03/04 | Unit | `HashingContrasenaTests` | Alta |
| 21 | SincronizarPassword → BCrypt | C-03 | Unit | `HashingContrasenaTests` | Alta |
| 22a | SA login correcto → ok | C-05 | Unit | `AuthServiceTests` | Alta |
| 22b | SA login incorrecto → UnauthorizedException | C-05 | Unit | `AuthServiceTests` | Alta |
| 23 | HoraDesde inválida → InvalidOperationException | B-03 | Unit | `HorarioFormatoTests` | Media |
| 24 | Horas válidas → sin excepción | B-03 | Unit | `HorarioFormatoTests` | Baja |
| 25 | Link reset usa `#token=` no `?token=` | B-04 | Unit | `TokenResetUrlTests` | Alta |
| 26 | `UpdateServicioDto` sin propiedad Activo | M-01 | Unit | `OverpostingTests` | Media |
| 27 | `UpdateSucursalDto` sin propiedad Activa | M-01 | Unit | `OverpostingTests` | Media |
| 28 | `CreateTurnoDto` campos Required ausentes → invalid | M-02 | Unit | `DtoValidacionesTests` | Media |
| 29 | `CreateClienteDto` nombre vacío → invalid | M-02 | Unit | `DtoValidacionesTests` | Media |
| 30 | `CreateServicioDto` precio negativo → invalid | M-02 | Unit | `DtoValidacionesTests` | Media |
| 31 | `CreateServicioDto` duración = 0 → invalid | M-02 | Unit | `DtoValidacionesTests` | Media |

**Total: 31 tests** — 10 HTTP · 5 Service · 16 Unit

---

## Proyectos de destino

| Proyecto | Carpeta | Tests |
|----------|---------|-------|
| `TurneroApp.IntegrationTests` | `Security/` | Tests HTTP (1–11) y Service (12–16) |
| `TurneroApp.Tests` | `Security/` | Tests Unit (17–31) |

---

## Decisiones de diseño

### ¿Por qué `WebApplicationFactory` para los tests HTTP?

Los tests de autorización (C-02, A-01, A-02) y de concurrencia (M-03) **requieren** que el stack
HTTP completo esté activo: middleware de autenticación JWT, rate limiter, políticas de autorización,
y el pipeline de ASP.NET Core que resuelve `[Authorize]` / `[AllowAnonymous]`.

Un test que instancie el servicio directamente **nunca** ejercitaría el middleware de autorización —
la protección podría romperse sin que ningún test lo detecte.

`WebApplicationFactory<Program>` levanta la aplicación real en memoria con `TestServer`, sin
abrir ningún puerto TCP, y permite reemplazar configuración (cadena de conexión, secrets) vía
`ConfigureAppConfiguration`. El resultado es indistinguible de producción desde el punto de vista
del middleware.

### ¿Por qué Testcontainers en el fixture HTTP?

El fixture de servicio ya usa `PostgresFixture` con Testcontainers. Para los tests HTTP necesitamos
una DB real por las mismas razones: las migraciones, los global query filters de EF, y la
transacción de serializable de M-03 solo funcionan sobre Postgres real (no SQLite ni in-memory).

`HttpTestFixture` embebe su propio `PostgreSqlContainer` para mantener el aislamiento del
`WebApplicationFactory` (que controla toda la configuración via `ConfigureAppConfiguration`).

### ¿Por qué deshabilitar el rate limiter por defecto en `HttpTestFixture`?

Los tests de autorización lanzan múltiples requests en serie. Si el rate limiter estuviera activo,
el test 3 (teoría con 5 endpoints) podría disparar un 429 en lugar del 401 esperado, causando
falsos negativos.

`RateLimitingTests` usa su propio fixture (`RateLimitTestFixture`) que **no** deshabilita el
rate limiter, asegurando que ese comportamiento se testee de forma aislada.

### ¿Por qué service-level para RowVersion (B-02)?

El test de concurrencia optimista necesita dos `DbContext` distintos operando sobre los mismos
registros. Con `WebApplicationFactory` cada request tiene su propio scope/DbContext, pero no
podemos controlar el momento exacto de `SaveChanges` de cada contexto para forzar la carrera.
A nivel de servicio podemos orquestar los dos contextos con precisión quirúrgica.

### Ubicación de los archivos de test

```
backend/tests/TurneroApp.IntegrationTests/
  Infrastructure/
    HttpTestFixture.cs          ← WebApplicationFactory<Program>
    HttpIntegrationTestBase.cs  ← clase base para tests HTTP
    TokenHelper.cs              ← generador de JWTs de test
  Security/
    AutorizacionHttpTests.cs
    IdorHttpTests.cs
    RateLimitingTests.cs
    ConflictoHorarioConcurrenteTests.cs
    ConcurrenciaOptimistaTests.cs
    QueryFiltersTests.cs

backend/tests/TurneroApp.Tests/
  Security/
    HmacOAuthTests.cs
    HashingContrasenaTests.cs
    HorarioFormatoTests.cs
    TokenResetUrlTests.cs
    OverpostingTests.cs
    DtoValidacionesTests.cs
```

---

## Cómo correr cada grupo

```bash
# Todos los tests de seguridad (integración + unit)
dotnet test --filter "Category=Security"

# Solo tests HTTP (requieren Docker)
dotnet test --project tests/TurneroApp.IntegrationTests --filter "Category=Http"

# Solo tests de concurrencia
dotnet test --project tests/TurneroApp.IntegrationTests --filter "Category=Concurrencia"

# Solo tests unitarios (sin Docker)
dotnet test --project tests/TurneroApp.Tests --filter "Category=Unit"

# Todos los tests
dotnet test TurneroApp.sln
```

> Los tests HTTP y de servicio requieren Docker en ejecución (Testcontainers levanta Postgres).
> Los tests unitarios no tienen dependencias externas.
