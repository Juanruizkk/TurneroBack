# Tests HTTP de Seguridad

Tests de integración que verifican las correcciones de seguridad al nivel HTTP, con el pipeline completo de ASP.NET Core activo (autenticación, autorización, rate limiting, middleware).

**Proyecto:** `TurneroApp.IntegrationTests`
**Carpeta:** `Security/`
**Infraestructura:** `HttpTestFixture` + `HttpIntegrationTestBase` (ver [README.md](README.md))
**Colección xUnit:** `"HttpTests"` (comparte contenedor y TestServer entre todas las clases)

---

## Hallazgos cubiertos

| Hallazgo | Descripción | Tests |
|---|---|---|
| C-02 | `SuperAdminController` requiere `[Authorize(Policy="SuperAdmin")]` | 1, 2 |
| A-01 | Endpoints de staff requieren `[Authorize]`; endpoints públicos no | 3, 4 |
| A-02 | IDOR: acceso cruzado entre sucursales bloqueado | 5, 6 |
| A-06 | Campo `Origen` asignado server-side según autenticación | 7, 8 |
| M-06 | Rate limiting en `/api/auth/login` (10 req / 15 min por IP) | 9, 10 |
| M-03 | Transacción Serializable previene race condition TOCTOU en turnos | 11 |

---

## Clase: `AutorizacionHttpTests`

**Archivo:** `Security/AutorizacionHttpTests.cs`
**Hallazgos:** C-02, A-01, A-06

---

### Test 1 — C-02: SuperAdminController sin token → 401

```
SuperAdmin_SinToken_Devuelve401
```

Llama a `GET /api/superadmin/sucursales` sin `Authorization` header.

**Assert:** `401 Unauthorized` — el endpoint tiene `[Authorize(Policy="SuperAdmin")]` y la política rechaza requests no autenticados.

---

### Test 2 — C-02: SuperAdminController con token de usuario normal → 403

```
SuperAdmin_ConTokenNormal_Devuelve403
```

Token válido pero con `esSuperAdmin=false`. Llama a `GET /api/superadmin/sucursales`.

**Assert:** `403 Forbidden` — el usuario está autenticado pero no cumple la política `SuperAdmin`.

---

### Test 3 — A-01: Endpoints de staff sin token → 401

```
EndpointsDeStaff_SinToken_Devuelven401 [Theory]
```

Envía requests sin token a: `GET /api/sucursales`, `/api/servicios`, `/api/clientes`, `/api/usuarios`, `/api/turnos`.

**Assert:** Todos devuelven `401 Unauthorized`. Estos controllers tienen `[Authorize]` a nivel clase.

---

### Test 4 — A-01: Endpoints públicos sin token → nunca 401

```
EndpointsPublicos_SinToken_NoDevuelven401 [Theory]
```

Llama sin token a: `GET /api/turnos/by-token/TOKEN_INEXISTENTE` y `GET /api/publico/slug-inexistente/perfil`.

**Assert:** Ninguno devuelve `401`. Pueden devolver `404` o `400`, pero no deben requerir autenticación.

---

### Test 7 — A-06: POST /api/turnos sin token → Origen = Web

```
CrearTurno_SinToken_OrigenEsWeb
```

**Arrange:** Seed completo con plan activo, suscripción, cliente, servicio y profesional (`SeedProfesionalConUSRAsync`). Request sin token.

**Act:** `POST /api/turnos` con payload válido.

**Assert:**
- Status `201 Created` o `200 OK`
- En DB: `turno.Origen == OrigenTurno.Web`

**Por qué es importante:** El campo `Origen` no viene del cliente; lo asigna el controller según `User.Identity.IsAuthenticated`. Sin token → `Web`. Esto previene que un cliente web se haga pasar por staff administrativo (A-06).

---

### Test 8 — A-06: POST /api/turnos con token → Origen = Administrativo

```
CrearTurno_ConToken_OrigenEsAdministrativo
```

Mismo arrange que Test 7 pero con `UsarToken(TokenHelper.ParaSucursal(sucursalId))`.

**Assert:**
- Status `201 Created` o `200 OK`
- En DB: `turno.Origen == OrigenTurno.Administrativo`

---

## Clase: `IdorHttpTests`

**Archivo:** `Security/IdorHttpTests.cs`
**Hallazgo:** A-02 (Insecure Direct Object Reference)

---

### Test 5 — A-02: Token de sucursal A accediendo a ajustes de sucursal B → 403

```
Ajustes_TokenSucursalA_AccedeSucursalB_Devuelve403
```

**Arrange:** Seed de dos sucursales reales en DB. Token firmado con `sucursalId = sucursalA.Id`.

**Act:** `GET /api/ajustes?sucursalId={sucursalB.Id}`

**Assert:** `403 Forbidden` — `AjustesController.ValidarAccesoSucursal` compara el `sucursalId` del claim JWT con el del query param. Si difieren, devuelve `Forbid()`.

---

### Test 6 — A-02: SuperAdmin puede acceder a cualquier sucursal → no 403

```
Ajustes_SuperAdmin_PuedeAccederCualquierSucursal_NoDevuelve403
```

**Arrange:** Seed de una sucursal. Token de SuperAdmin (`TokenHelper.SuperAdmin()`).

**Act:** `GET /api/ajustes?sucursalId={sucursal.Id}`

**Assert:** Status ≠ `403`. `ValidarAccesoSucursal` retorna `null` (ok) cuando el claim `esSuperAdmin=true`, saltando la verificación de sucursal.

---

## Clase: `RateLimitingTests`

**Archivo:** `Security/RateLimitingTests.cs`
**Hallazgo:** M-06 (Rate Limiting en /api/auth/login)
**Fixture especial:** `RateLimitTestFixture` — hereda de `HttpTestFixture` pero NO sobreescribe los servicios, preservando el rate limiter de producción.

---

### Test 9 — M-06: 11 intentos de login → 429 en el último

```
Login_MasDe10Intentos_Devuelve429
```

Envía 11 requests consecutivos a `POST /api/auth/login` con credenciales inválidas (`x@x.com` / `wrong`). Cada request crea un nuevo `StringContent` (no se puede reenviar el mismo objeto `HttpContent`).

**Assert:** El 11mo (último) request devuelve `429 Too Many Requests`. La política `"auth"` permite 10 requests por ventana de 15 minutos por IP.

---

### Test 10 — M-06: Rate limit solo aplica a /api/auth/login

```
EndpointsNoAuth_SinRateLimit_NoDevuelven429
```

Envía 15 requests concurrentes (`Task.WhenAll`) a `GET /api/sucursales` con un token válido.

**Assert:** Ninguno devuelve `429`. El rate limiter está configurado exclusivamente para el endpoint de login (política `"auth"`); los demás endpoints no tienen política aplicada.

---

## Clase: `ConflictoHorarioConcurrenteTests`

**Archivo:** `Security/ConflictoHorarioConcurrenteTests.cs`
**Hallazgo:** M-03 (Race Condition / TOCTOU en creación de turnos)
**Fixture:** `HttpTestFixture` estándar (colección `"HttpTests"`)

---

### Test 11 — M-03: 5 requests simultáneos para el mismo slot → solo 1 persiste

```
CrearTurno_ConcurrenteMismoHorario_SoloUnoSePersiste
```

**Arrange:** Seed completo con plan, sucursal, suscripción, cliente, servicio y profesional con USR. Slot fijo: `DateTime.UtcNow.AddDays(5).Date.AddHours(10)`.

**Act:** 5 `HttpClient` independientes (cada uno con `Factory.CreateClient()`) envían simultáneamente `POST /api/turnos` con el mismo payload, usando `Task.WhenAll`.

**Assert:**
- Exactamente **1** response con `201 Created` o `200 OK`
- Exactamente **4** responses con `409 Conflict`
- En DB: exactamente **1** turno para ese slot y profesional (`IgnoreQueryFilters`)

**Por qué HTTP y no llamada directa al servicio:**
Con la transacción `SERIALIZABLE` de PostgreSQL, el aislamiento requiere que cada request tenga su propia conexión de BD independiente — exactamente lo que hace `Task.WhenAll` sobre múltiples `HttpClient`. Llamar al servicio directamente con el mismo `DbContext` no replicaría el escenario real de producción.

**Cómo funciona el fix M-03:**
`TurnoService.CreateAsync` envuelve la verificación de conflicto + inserción en una transacción `IsolationLevel.Serializable`. PostgreSQL detecta que dos transacciones concurrentes leen y modifican el mismo conjunto de filas, y aborta todas excepto una con `serialization failure` (error 40001). El servicio traduce esa excepción a `InvalidOperationException` → el controller responde `409 Conflict`.

---

## Configuración de autenticación en tests

El `HttpTestFixture` configura la API en modo `"Testing"` e inyecta un JWT secret conocido:

```
Jwt:Secret   = TestConstants.JwtSecret  ("test-secret-key-32-chars-minimum!!")
Jwt:Issuer   = "TurneroApp"
Jwt:Audience = "TurneroApp"
```

`TokenHelper` genera JWTs firmados con ese secret. Los claims relevantes son:
- `userId` — GUID aleatorio
- `sucursalId` — GUID de la sucursal (cuando aplica)
- `esSuperAdmin` — `"true"` o `"false"`
- `ClaimTypes.Role` — `"Administrativo"` por defecto, `"SuperAdmin"` para admins

Los tokens generados en tests son aceptados por la API porque el secret coincide con el configurado en `ConfigureAppConfiguration`.
