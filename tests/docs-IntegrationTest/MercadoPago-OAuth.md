# MercadoPago OAuth — Documentación completa

Integración de OAuth 2.0 con Mercado Pago para que cada sucursal conecte su propia cuenta y cobre con su propio `access_token`.

---

## Índice

1. [Por qué OAuth y no un token global](#1-por-qué-oauth-y-no-un-token-global)
2. [Mapa de archivos](#2-mapa-de-archivos)
3. [Cómo se conectan todas las capas](#3-cómo-se-conectan-todas-las-capas)
4. [Flujo completo paso a paso](#4-flujo-completo-paso-a-paso)
   - [Flujo 1 — Conexión inicial](#flujo-1--conexión-inicial)
   - [Flujo 2 — Uso del token para cobrar](#flujo-2--uso-del-token-para-cobrar)
   - [Flujo 3 — Refresh automático](#flujo-3--refresh-automático)
5. [Referencia de cada archivo](#5-referencia-de-cada-archivo)
6. [Tabla de la base de datos](#6-tabla-de-la-base-de-datos)
7. [Configuración (appsettings)](#7-configuración-appsettings)
8. [Registro en Program.cs](#8-registro-en-programcs)
9. [Endpoints HTTP](#9-endpoints-http)
10. [Reglas críticas de la API de MP](#10-reglas-críticas-de-la-api-de-mp)
11. [Ciclo de vida de los tokens](#11-ciclo-de-vida-de-los-tokens)
12. [Próximo paso — integrar con PagoTurnoService](#12-próximo-paso--integrar-con-pagoturnoservice)

---

## 1. Por qué OAuth y no un token global

El sistema anterior usaba **un único `AccessToken` global** en `appsettings.json` (`MercadoPago:AccessToken`). Eso significa que todos los cobros se acreditan en la misma cuenta MP sin importar qué sucursal genera el turno.

Con OAuth cada sucursal:
- Conecta **su propia cuenta de Mercado Pago**
- Los cobros se acreditan directamente en esa cuenta
- El backend solo actúa como intermediario (plataforma)

El flujo correcto para plataformas es **OAuth 2.0 — Authorization Code**, que es lo que implementa este módulo.

---

## 2. Mapa de archivos

```
src/
├── Domain/
│   └── Entities/
│       └── SucursalMercadoPago.cs              ← Entidad: tokens por sucursal
│
├── Application/
│   ├── Interfaces/
│   │   ├── IMercadoPagoOAuthService.cs         ← Contrato del servicio OAuth
│   │   └── ISucursalMercadoPagoRepository.cs   ← Contrato del repositorio
│   └── DTOs/
│       └── MercadoPago/
│           └── MercadoPagoOAuthTokenResponse.cs ← DTO de respuesta de MP
│
└── Infrastructure/
    ├── Data/
    │   ├── AppDbContext.cs                      ← DbSet<SucursalMercadoPago> agregado
    │   └── Configurations/
    │       └── SucursalMercadoPagoConfiguration.cs ← Mapeo tabla PostgreSQL
    ├── Repositories/
    │   └── SucursalMercadoPagoRepository.cs     ← Implementación del repo
    └── Services/
        └── MercadoPago/
            ├── MercadoPagoOAuthOptions.cs       ← Configuración (appsettings)
            ├── MercadoPagoOAuthService.cs       ← Lógica OAuth completa
            │
            │   (existentes, sin modificar)
            ├── MercadoPagoOptions.cs            ← Token global (legacy)
            └── MercadoPagoGateway.cs            ← Gateway de pagos (legacy)

src/Api/
├── Controllers/
│   └── MercadoPagoOAuthController.cs           ← 3 endpoints HTTP
├── appsettings.json                             ← Sección MercadoPagoOAuth
└── Program.cs                                  ← Registro de servicios
```

---

## 3. Cómo se conectan todas las capas

```
┌─────────────────────────────────────────────────────────────────┐
│  CAPA API                                                       │
│                                                                 │
│  MercadoPagoOAuthController                                     │
│    ├── [GET]  /api/mercadopago/oauth/start?sucursalId=...       │
│    ├── [GET]  /api/mercadopago/oauth/callback?code=&state=      │
│    └── [POST] /api/mercadopago/oauth/{sucursalId}/refresh       │
│                                                                 │
│  Depende de:  IMercadoPagoOAuthService (inyectado)             │
└──────────────────────────┬──────────────────────────────────────┘
                           │ interfaz
┌──────────────────────────▼──────────────────────────────────────┐
│  CAPA APPLICATION (Interfaces y DTOs)                           │
│                                                                 │
│  IMercadoPagoOAuthService                                       │
│    ├── GenerarUrlAutorizacion(sucursalId) → string              │
│    ├── ConectarAsync(sucursalId, code)    → Task                │
│    ├── ObtenerAccessTokenVigenteAsync(sucursalId) → Task<string>│
│    └── RefrescarTokenAsync(sucursalId)   → Task                 │
│                                                                 │
│  ISucursalMercadoPagoRepository (: IGenericRepository<T>)      │
│    └── GetBySucursalIdAsync(sucursalId)  → Task<SucursalMP?>   │
│                                                                 │
│  MercadoPagoOAuthTokenResponse (DTO)                           │
│    ├── AccessToken, RefreshToken, ExpiresIn                     │
│    ├── UserId, TokenType, Scope, PublicKey                      │
│    └── [JsonPropertyName] para deserializar snake_case de MP   │
└──────────────┬────────────────────────────┬─────────────────────┘
               │ implementa                  │ implementa
┌──────────────▼──────────┐   ┌─────────────▼───────────────────┐
│  MercadoPagoOAuthService│   │  SucursalMercadoPagoRepository  │
│  (Infrastructure)       │   │  (Infrastructure)               │
│                         │   │                                 │
│  Deps:                  │   │  Extiende: GenericRepository<T> │
│  · HttpClient           │   │  · GetBySucursalIdAsync()       │
│  · MercadoPagoOAuth     │   │    → FirstOrDefaultAsync()      │
│    Options              │   │    sobre DbSet<SucursalMP>      │
│  · ISucursalMPRepo      │   │                                 │
│                         │   │  Hereda:                        │
│  Método privado:        │   │  · AddAsync, Update,            │
│  PostTokenAsync()       │   │    SaveChangesAsync             │
│  → FormUrlEncoded POST  │   │                                 │
│  → deserializa JSON resp│   └──────────────┬──────────────────┘
└─────────────────────────┘                  │ usa
                                ┌────────────▼───────────────────┐
                                │  AppDbContext                  │
                                │                                │
                                │  DbSet<SucursalMercadoPago>    │
                                │  SucursalMercadoPagos          │
                                │                                │
                                │  Configuración EF aplicada     │
                                │  vía ApplyConfigurationsFrom   │
                                │  Assembly() automáticamente    │
                                └────────────┬───────────────────┘
                                             │ mapea a
                                ┌────────────▼───────────────────┐
                                │  PostgreSQL                    │
                                │  tabla: sucursal_mercadopago   │
                                │  (ver sección 6)               │
                                └────────────────────────────────┘
```

---

## 4. Flujo completo paso a paso

### Flujo 1 — Conexión inicial

```
Usuario (owner de sucursal)
  │
  │  1. Hace click en "Conectar Mercado Pago" en /ajustes
  │     El frontend navega o hace redirect a:
  │
  ▼
GET /api/mercadopago/oauth/start?sucursalId={GUID}
[Authorize — requiere JWT válido]
  │
  │  2. MercadoPagoOAuthController.Start()
  │     llama a: _oauthService.GenerarUrlAutorizacion(sucursalId)
  │
  ▼
MercadoPagoOAuthService.GenerarUrlAutorizacion()
  │
  │  3. Construye la URL de autorización:
  │     https://auth.mercadopago.com/authorization
  │       ?client_id=TU_CLIENT_ID
  │       &response_type=code
  │       &platform_id=mp
  │       &state={sucursalId:N}   ← GUID sin guiones, identifica la sucursal
  │       &redirect_uri=https://api.tudominio.com/api/mercadopago/oauth/callback
  │
  │  4. Controller retorna HTTP 302 → redirige al navegador a MP
  │
  ▼
[MERCADO PAGO]
  │
  │  5. El usuario inicia sesión en su cuenta MP y autoriza la app
  │
  │  6. MP redirige de vuelta a:
  │     https://api.tudominio.com/api/mercadopago/oauth/callback
  │       ?code=SERVER_GENERATED_CODE    ← dura 10 minutos, uso único
  │       &state={sucursalId:N}
  │
  ▼
GET /api/mercadopago/oauth/callback?code=...&state=...
[AllowAnonymous — MP no puede enviar JWT]
  │
  │  7. Controller valida:
  │     - code no está vacío
  │     - state es un GUID válido formato "N" → recupera sucursalId
  │
  │  8. Llama a: _oauthService.ConectarAsync(sucursalId, code)
  │
  ▼
MercadoPagoOAuthService.ConectarAsync()
  │
  │  9. Llama a PostTokenAsync() con:
  │     POST https://api.mercadolibre.com/oauth/token
  │     Headers:
  │       accept: application/json
  │       content-type: application/x-www-form-urlencoded  ← CRÍTICO
  │     Body (form-encoded):
  │       client_id=...
  │       client_secret=...
  │       grant_type=authorization_code
  │       code=SERVER_GENERATED_CODE
  │       redirect_uri=...
  │
  │  10. MP responde JSON:
  │      {
  │        "access_token": "APP_USR-...",
  │        "refresh_token": "TG-...",
  │        "expires_in": 15552000,    ← ~180 días en segundos
  │        "user_id": "123456789",
  │        "token_type": "bearer",
  │        "scope": "offline_access payments ...",
  │        "public_key": "APP_USR-..."
  │      }
  │
  │  11. Deserializa a MercadoPagoOAuthTokenResponse
  │      (usa [JsonPropertyName] para mapear snake_case → PascalCase)
  │
  │  12. Busca registro existente con GetBySucursalIdAsync(sucursalId)
  │
  │  13a. Si NO existe → AddAsync(new SucursalMercadoPago { ... })
  │  13b. Si SÍ existe → Update(existente) con nuevos tokens
  │       (permite reconectar o cambiar cuenta)
  │
  │  14. SaveChangesAsync() → persiste en sucursal_mercadopago
  │
  ▼
  │  15. Controller redirige al frontend:
  │      302 → https://metaturnos.com/ajustes?mp=conectado
  │
  ▼
Usuario ve confirmación en el frontend
```

---

### Flujo 2 — Uso del token para cobrar

```
PagoTurnoService (futuro uso)
  │
  │  Cuando necesita generar una preferencia de pago para la sucursal X:
  │
  ▼
_mercadoPagoOAuthService.ObtenerAccessTokenVigenteAsync(sucursalId)
  │
  │  1. GetBySucursalIdAsync(sucursalId)
  │     → lanza InvalidOperationException si la sucursal no está conectada
  │
  │  2. Verifica: ¿AccessTokenExpiraEnUtc <= UtcNow + 5 minutos?
  │
  │  SI está por expirar:
  │    → llama RefrescarTokenAsync(sucursalId)  [ver Flujo 3]
  │    → vuelve a leer el registro actualizado de la DB
  │
  │  3. Retorna registro.AccessToken
  │
  ▼
string accessToken  ← token vigente listo para usar en SDK de MP
```

---

### Flujo 3 — Refresh automático

```
MercadoPagoOAuthService.RefrescarTokenAsync(sucursalId)
  │
  │  1. GetBySucursalIdAsync(sucursalId)
  │     → recupera el registro con el refresh_token actual
  │
  │  2. PostTokenAsync() con:
  │     POST https://api.mercadolibre.com/oauth/token
  │     Body (form-encoded):
  │       client_id=...
  │       client_secret=...
  │       grant_type=refresh_token      ← cambia respecto al inicial
  │       refresh_token=TG-...          ← token actual
  │       redirect_uri=...
  │
  │  3. MP devuelve nuevos access_token y refresh_token
  │
  │  4. Actualiza el registro:
  │     · AccessToken             = nuevo
  │     · RefreshToken            = nuevo (MP rota el refresh_token)
  │     · AccessTokenExpiraEnUtc  = UtcNow + ExpiresIn segundos
  │     · RefreshTokenExpiraEnUtc = UtcNow + 6 meses
  │     · UltimaActualizacionUtc  = UtcNow
  │
  │  5. _repo.Update() + SaveChangesAsync()
  │
  ▼
Token actualizado en DB, listo para el próximo uso
```

---

## 5. Referencia de cada archivo

### `SucursalMercadoPago.cs` — Domain/Entities

Entidad pura sin dependencias externas. Representa la conexión OAuth de una sucursal con MP.

| Propiedad | Tipo | Descripción |
|---|---|---|
| `Id` | `Guid` | PK, asignado manualmente |
| `SucursalId` | `Guid` | FK a `sucursales.Id`, unique |
| `MpUserId` | `string` | ID del usuario en MP (viene en `user_id`) |
| `AccessToken` | `string` | Token de acceso activo |
| `RefreshToken` | `string` | Token para obtener nuevo AccessToken |
| `AccessTokenExpiraEnUtc` | `DateTime` | UtcNow + ExpiresIn segundos al momento de obtenerlo |
| `RefreshTokenExpiraEnUtc` | `DateTime` | UtcNow + 6 meses (según doc MP) |
| `FechaConexionUtc` | `DateTime` | Timestamp de la primera conexión |
| `UltimaActualizacionUtc` | `DateTime?` | Timestamp del último refresh |
| `Activo` | `bool` | `true` siempre que la conexión sea válida |
| `Sucursal` | `Sucursal` | Navigation property |

---

### `IMercadoPagoOAuthService.cs` — Application/Interfaces

Contrato que la capa Application expone. El controller y cualquier futuro servicio dependen de esta interfaz, nunca de la implementación concreta.

```
GenerarUrlAutorizacion(Guid sucursalId) → string
  Puro, sin I/O. Construye la URL de MP con los query params correctos.

ConectarAsync(Guid sucursalId, string code) → Task
  Llama a MP, persiste tokens. Lanza InvalidOperationException si MP falla.

ObtenerAccessTokenVigenteAsync(Guid sucursalId) → Task<string>
  Punto de entrada para PagoTurnoService. Refresca automáticamente si es necesario.

RefrescarTokenAsync(Guid sucursalId) → Task
  Usa grant_type=refresh_token. Puede llamarse manualmente desde el endpoint /refresh.
```

---

### `ISucursalMercadoPagoRepository.cs` — Application/Interfaces

Extiende `IGenericRepository<SucursalMercadoPago>`, que provee:
- `AddAsync`, `Update`, `SaveChangesAsync`, `GetByIdAsync`, `Delete`

Agrega el método específico:
```
GetBySucursalIdAsync(Guid sucursalId) → Task<SucursalMercadoPago?>
  Busca por SucursalId (no por Id de la entidad).
  Retorna null si la sucursal nunca se conectó.
```

---

### `MercadoPagoOAuthTokenResponse.cs` — Application/DTOs/MercadoPago

DTO de deserialización. MP devuelve JSON en `snake_case`, .NET usa `PascalCase`.
Los `[JsonPropertyName]` resuelven esto sin configuración global de `JsonSerializerOptions`.

```json
// Lo que devuelve MP:
{
  "access_token": "APP_USR-...",
  "refresh_token": "TG-...",
  "expires_in": 15552000,
  "user_id": "123456",
  "token_type": "bearer",
  "scope": "offline_access payments read write",
  "public_key": "APP_USR-..."
}

// Lo que lee el DTO:
response.AccessToken   ← "APP_USR-..."
response.RefreshToken  ← "TG-..."
response.ExpiresIn     ← 15552000
response.UserId        ← "123456"
```

---

### `SucursalMercadoPagoConfiguration.cs` — Infrastructure/Data/Configurations

EF Core Fluent API. Se aplica automáticamente vía `ApplyConfigurationsFromAssembly()` en `AppDbContext`.

Decisiones de configuración relevantes:

| Decisión | Valor | Motivo |
|---|---|---|
| Tabla | `sucursal_mercadopago` | Convención snake_case del proyecto |
| `Id` | `ValueGeneratedNever()` | Todos los IDs son manuales en este proyecto |
| `AccessToken` | `MaxLength(1000)` | Los tokens de MP pueden ser largos |
| `SucursalId` | Unique index | 1 sucursal = 1 conexión MP activa |
| `OnDelete` | `Cascade` | Si se borra la sucursal, se borran sus tokens |
| Fechas | `timestamptz` | Siempre UTC, tipo correcto en PostgreSQL |

---

### `SucursalMercadoPagoRepository.cs` — Infrastructure/Repositories

Hereda de `GenericRepository<SucursalMercadoPago>`. Solo agrega `GetBySucursalIdAsync`:

```csharp
// Query real en PostgreSQL:
SELECT * FROM sucursal_mercadopago
WHERE sucursal_id = @sucursalId
LIMIT 1
```

No aplica global query filter (la entidad no tiene filtro multi-tenant, porque el token es de la sucursal, no de un usuario).

---

### `MercadoPagoOAuthOptions.cs` — Infrastructure/Services/MercadoPago

Options pattern. Cargado desde `appsettings.json → "MercadoPagoOAuth"`.

| Propiedad | Valor por defecto | Descripción |
|---|---|---|
| `ClientId` | — | ID de tu app en el panel de MP |
| `ClientSecret` | — | Secret de tu app en MP |
| `RedirectUri` | — | Debe coincidir **exactamente** con la configurada en MP |
| `AuthorizationBaseUrl` | `https://auth.mercadopago.com/authorization` | URL de autorización |
| `TokenUrl` | `https://api.mercadolibre.com/oauth/token` | Endpoint de tokens (ML = MP, misma plataforma) |

---

### `MercadoPagoOAuthService.cs` — Infrastructure/Services/MercadoPago

Implementación concreta. Registrada con `AddHttpClient<IMercadoPagoOAuthService, MercadoPagoOAuthService>()` para que el `HttpClient` sea gestionado por el pool de .NET.

**Método clave: `PostTokenAsync()`**

```
INPUT:  Dictionary<string, string> con los parámetros del form

1. Crea HttpRequestMessage con FormUrlEncodedContent
   → content-type: application/x-www-form-urlencoded   ← lo que MP exige
2. Agrega header: accept: application/json
3. Llama _httpClient.SendAsync()
4. Si HTTP error → lanza InvalidOperationException con el body
5. Deserializa con JsonSerializer (PropertyNameCaseInsensitive = true)

OUTPUT: MercadoPagoOAuthTokenResponse
```

**Por qué `FormUrlEncodedContent` y no `PostAsJsonAsync`:**
La documentación oficial de MP especifica explícitamente que el `content-type` debe ser `application/x-www-form-urlencoded`. Usar `application/json` o `multipart/form-data` retorna error.

---

### `MercadoPagoOAuthController.cs` — Api/Controllers

Tres endpoints bajo `api/mercadopago/oauth`:

| Endpoint | Auth | Descripción |
|---|---|---|
| `GET /start?sucursalId={guid}` | `[Authorize]` | Redirige a MP. Requiere JWT porque solo el owner inicia el flujo |
| `GET /callback?code=&state=` | `[AllowAnonymous]` | MP llama aquí. No puede enviar JWT |
| `POST /{sucursalId}/refresh` | `[Authorize]` | Fuerza refresh manual del token |

El callback redirige al frontend a `/ajustes?mp=conectado` para que el frontend muestre confirmación.

---

## 6. Tabla de la base de datos

```sql
-- sucursal_mercadopago
CREATE TABLE sucursal_mercadopago (
    id                          UUID PRIMARY KEY,
    sucursal_id                 UUID NOT NULL REFERENCES sucursales(id) ON DELETE CASCADE,
    mp_user_id                  VARCHAR(100) NOT NULL,
    access_token                VARCHAR(1000) NOT NULL,
    refresh_token               VARCHAR(1000) NOT NULL,
    access_token_expira_en_utc  TIMESTAMPTZ NOT NULL,
    refresh_token_expira_en_utc TIMESTAMPTZ NOT NULL,
    fecha_conexion_utc          TIMESTAMPTZ NOT NULL,
    ultima_actualizacion_utc    TIMESTAMPTZ,
    activo                      BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE UNIQUE INDEX ix_sucursal_mercadopago_sucursal_id
    ON sucursal_mercadopago (sucursal_id);
```

El índice único en `sucursal_id` garantiza que una sucursal solo tenga un registro activo. Si reconecta, el servicio hace `UPDATE` sobre el registro existente.

---

## 7. Configuración (appsettings)

### `appsettings.json` — valores de producción

```json
"MercadoPagoOAuth": {
  "ClientId": "TU_CLIENT_ID",
  "ClientSecret": "TU_CLIENT_SECRET",
  "RedirectUri": "https://api.tudominio.com/api/mercadopago/oauth/callback",
  "AuthorizationBaseUrl": "https://auth.mercadopago.com/authorization",
  "TokenUrl": "https://api.mercadolibre.com/oauth/token"
}
```

### `appsettings.Development.json` — valores para desarrollo

```json
"MercadoPagoOAuth": {
  "ClientId": "TU_CLIENT_ID_SANDBOX",
  "ClientSecret": "TU_CLIENT_SECRET_SANDBOX",
  "RedirectUri": "https://TU-NGROK.ngrok-free.app/api/mercadopago/oauth/callback"
}
```

> En desarrollo la `RedirectUri` debe ser pública. Usá ngrok:
> ```bash
> ngrok http 5000
> # Luego copiá la URL https://xxx.ngrok-free.app
> ```

**Dónde obtener `ClientId` y `ClientSecret`:**
1. Entrá al [Panel de Mercado Pago Developers](https://www.mercadopago.com.ar/developers)
2. Tus aplicaciones → creá o seleccioná tu app
3. Credenciales → `APP_ID` = `ClientId`, `Client Secret` = `ClientSecret`
4. Configurá la `RedirectUri` exactamente igual en el panel de MP

---

## 8. Registro en Program.cs

```csharp
// ─── MercadoPago OAuth ───────────────────────────────────────────────────────
builder.Services.Configure<MercadoPagoOAuthOptions>(
    builder.Configuration.GetSection(MercadoPagoOAuthOptions.SectionName));

builder.Services.AddScoped<ISucursalMercadoPagoRepository, SucursalMercadoPagoRepository>();

// AddHttpClient gestiona el pool de conexiones HTTP (no crear HttpClient manualmente)
builder.Services.AddHttpClient<IMercadoPagoOAuthService, MercadoPagoOAuthService>();
```

**Por qué `AddHttpClient` y no `AddScoped`:**
`AddHttpClient` registra el servicio como `Scoped` pero además crea y gestiona el `HttpClient` a través de `IHttpClientFactory`. Esto evita el problema de socket exhaustion que ocurre cuando se crea un `HttpClient` por request.

---

## 9. Endpoints HTTP

### `GET /api/mercadopago/oauth/start`

```http
GET /api/mercadopago/oauth/start?sucursalId=3fa85f64-5717-4562-b3fc-2c963f66afa6
Authorization: Bearer {jwt_token}
```

**Respuesta:** `302 Found` → Location: `https://auth.mercadopago.com/authorization?client_id=...`

---

### `GET /api/mercadopago/oauth/callback`

MP llama a este endpoint automáticamente. No lo llama el frontend.

```http
GET /api/mercadopago/oauth/callback?code=TG-ABC123&state=3fa85f645717...
```

**Respuestas:**
- `302 Found` → `{frontendUrl}/ajustes?mp=conectado` (éxito)
- `400 Bad Request` → si `code` es null o `state` es inválido
- `400 Bad Request` → si MP devuelve error al intercambiar el code

---

### `POST /api/mercadopago/oauth/{sucursalId}/refresh`

```http
POST /api/mercadopago/oauth/3fa85f64-5717-4562-b3fc-2c963f66afa6/refresh
Authorization: Bearer {jwt_token}
```

**Respuestas:**
- `204 No Content` (éxito)
- `404 Not Found` → si la sucursal no tiene conexión MP

---

## 10. Reglas críticas de la API de MP

Extraídas de la documentación oficial:

| Regla | Correcto | Incorrecto |
|---|---|---|
| Content-Type del POST a `/oauth/token` | `application/x-www-form-urlencoded` | `application/json` / `multipart/form-data` |
| Headers permitidos | Solo `accept` y `content-type` | No agregar headers extra |
| `grant_type` válidos | `authorization_code` / `refresh_token` | Cualquier otro valor |
| Parámetros del POST | Solo en el body (form) | No en Query Params |
| `redirect_uri` | URL estática, idéntica a la configurada en el panel MP | URLs dinámicas con query params |
| Parámetros adicionales | Usar el campo `state` | No modificar `redirect_uri` |
| `code` | Uso único | No reusar — dura solo 10 minutos |

---

## 11. Ciclo de vida de los tokens

```
[Conexión inicial]
  │
  ├── access_token    → expira en ~180 días (ExpiresIn segundos desde conexión)
  └── refresh_token   → expira en ~6 meses (según doc MP)

[ObtenerAccessTokenVigenteAsync]
  │
  ├── Si faltan > 5 minutos para expirar → retorna el token actual
  └── Si faltan ≤ 5 minutos              → refresca y retorna el nuevo

[Refresh]
  │
  ├── Nuevo access_token      (MP genera uno nuevo)
  ├── Nuevo refresh_token     (MP rota el refresh también)
  ├── AccessTokenExpiraEnUtc  = UtcNow + ExpiresIn
  └── RefreshTokenExpiraEnUtc = UtcNow + 6 meses

[Si el refresh_token también expira]
  └── El usuario debe reconectar su cuenta desde /ajustes
      volviendo a hacer el flujo OAuth completo (Flujo 1)
```

---

## 12. Próximo paso — integrar con PagoTurnoService

El `MercadoPagoGateway` actual usa el token global de `appsettings.json`. Para usar el token de la sucursal:

```csharp
// En PagoTurnoService.GenerarLinkDePagoAsync():
// 1. Obtener el token vigente de la sucursal
var accessToken = await _mercadoPagoOAuthService
    .ObtenerAccessTokenVigenteAsync(turno.SucursalId);

// 2. Pasárselo al gateway
var preferenceId = await _mercadoPagoGateway
    .CrearPreferenciaAsync(turnoId, titulo, monto, frontendUrl, accessToken);
```

Esto requiere modificar `IMercadoPagoGateway.CrearPreferenciaAsync` para aceptar un `accessToken` opcional, y usar ese en lugar del global cuando esté disponible. Ese cambio es el siguiente paso lógico.
