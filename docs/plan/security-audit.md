# Security Audit — TurneroAPP Backend

**Fecha:** 2026-04-21
**Auditor:** Revisión automatizada + análisis manual
**Versión auditada:** .NET 8, commit actual en rama principal
**Clasificación:** CONFIDENCIAL — uso interno

---

## 1. Resumen Ejecutivo

Se encontraron **28 hallazgos** distribuidos en 4 niveles de severidad. Los 8 hallazgos críticos permiten comprometer la plataforma completa sin autenticación previa.

| Severidad | Cantidad | Estado    |
|-----------|----------|-----------|
| 🔴 CRÍTICO | 8        | ✅ Completado (Fase 1) |
| 🟠 ALTO    | 6        | ✅ Completado (Fase 2) |
| 🟡 MEDIO   | 9        | ✅ Completado 9/9 (Fase 3) |
| 🔵 BAJO    | 5        | ✅ Completado 5/5 (Fase 4) |
| **TOTAL**  | **28**   | **28/28 resueltos** |

### Riesgo principal
Un atacante sin credenciales puede:
1. Acceder a `POST /api/superadmin/sincronizar-password` y cambiar contraseñas de cualquier usuario.
2. Crear sucursales/owners con la contraseña que quiera (sin hashear).
3. Escalar su propio rol a `SuperAdmin` via `PUT /api/usuarios/{id}` con `{"Rol": "SuperAdmin"}`.
4. Manipular todos los datos de la plataforma sin ningún token.

---

## 2. Hallazgos Detallados

### 2.1 Autenticación y Credenciales

---

#### [C-01] Secrets hardcodeados en appsettings.json

**Severidad:** 🔴 CRÍTICO
**Archivo:** `src/Api/appsettings.json`, `src/Api/appsettings.Development.json`

**Descripción:**
Las siguientes claves sensibles están hardcodeadas en archivos de configuración que forman parte del repositorio:

- `JwtSettings:Secret` — clave de firma de JWT
- `SuperAdmin:DefaultPassword` — contraseña del superadmin
- `ResendSettings:ApiKey` — clave de API de correo
- `MercadoPago:ClientSecret` — credencial OAuth de MP
- `Cloudinary:ApiSecret` — clave de almacenamiento de imágenes
- Cadena de conexión con usuario y contraseña de la base de datos

**Impacto:**
Cualquier persona con acceso al repositorio (o que haya tenido acceso alguna vez) puede leer estas credenciales. Si el repo fue público aunque sea por un momento, las credenciales deben rotarse.

**Fix (Paso 1):**

```bash
# Paso 1: Habilitar user-secrets para desarrollo
cd src/Api
dotnet user-secrets init
dotnet user-secrets set "JwtSettings:Secret" "tu-secret-local-muy-largo-min-32-chars"
dotnet user-secrets set "SuperAdmin:DefaultPassword" "tu-password-local"
dotnet user-secrets set "ResendSettings:ApiKey" "re_xxxxx"
dotnet user-secrets set "MercadoPago:ClientSecret" "APP_USR-xxxxx"
dotnet user-secrets set "Cloudinary:ApiSecret" "xxxx"
```

En `appsettings.json` reemplazar valores reales por placeholders:
```json
{
  "JwtSettings": {
    "Secret": "REPLACE_WITH_ENV_VAR_OR_USER_SECRET"
  },
  "SuperAdmin": {
    "DefaultPassword": "REPLACE_WITH_ENV_VAR"
  }
}
```

En producción, usar variables de entorno:
```
JWTSETINGS__SECRET=valor
SUPERADMIN__DEFAULTPASSWORD=valor
```

---

#### [C-02] SuperAdminController sin [Authorize]

**Severidad:** 🔴 CRÍTICO
**Archivo:** `src/Api/Controllers/SuperAdminController.cs`

**Descripción:**
El controller que gestiona toda la plataforma (crear empresas, owners, sincronizar passwords, etc.) no tiene ningún atributo `[Authorize]`. Cualquier petición HTTP anónima puede ejecutar estos endpoints.

**Fix (Paso 2):**

```csharp
// SuperAdminController.cs
[ApiController]
[Route("api/superadmin")]
[Authorize]  // ← AGREGAR
public class SuperAdminController : ControllerBase
{
    // Al inicio de cada action que requiera ser SuperAdmin:
    private void ValidarSuperAdmin()
    {
        var esSuperAdmin = User.FindFirstValue("EsSuperAdmin");
        if (esSuperAdmin != "True")
            throw new UnauthorizedAccessException("Requiere rol SuperAdmin.");
    }
}
```

---

#### [C-03] SuperAdminService.SincronizarPasswordAsync guarda contraseña sin hashear

**Severidad:** 🔴 CRÍTICO
**Archivo:** `src/Application/Services/SuperAdminService.cs` (~línea 85)

**Descripción:**
El método asigna directamente el texto plano al campo `PasswordHash` sin pasar por BCrypt.

**Fix (Paso 3):**

```csharp
// ANTES (vulnerable):
usuario.PasswordHash = dto.NuevaPassword;

// DESPUÉS (correcto):
usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NuevaPassword);
```

---

#### [C-04] SuperAdminService.CrearConOwnerAsync guarda password del owner sin BCrypt

**Severidad:** 🔴 CRÍTICO
**Archivo:** `src/Application/Services/SuperAdminService.cs` (~línea 166)

**Descripción:**
Al crear una empresa con su owner inicial, la contraseña se asigna en texto plano.

**Fix (Paso 3 — mismo archivo):**

```csharp
// ANTES (vulnerable):
owner.PasswordHash = dto.OwnerPassword;

// DESPUÉS (correcto):
owner.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.OwnerPassword);
```

---

#### [C-05] AuthService compara contraseña SuperAdmin en texto plano

**Severidad:** 🔴 CRÍTICO
**Archivo:** `src/Application/Services/AuthService.cs`

**Descripción:**
La comparación del login del superadmin usa `==` en lugar de `BCrypt.Verify`, lo que sólo funciona si la contraseña NO está hasheada. Este bug es consecuencia directa de C-03/C-04, pero debe corregirse independientemente.

**Fix:**

```csharp
// ANTES (vulnerable):
if (usuario.PasswordHash == password)

// DESPUÉS (correcto):
if (!BCrypt.Net.BCrypt.Verify(password, usuario.PasswordHash))
    throw new UnauthorizedException("Credenciales inválidas.");
```

---

#### [C-06] UpdateUsuarioDto expone Rol + Estado + Permisos

**Severidad:** 🔴 CRÍTICO
**Archivo:** `src/Application/DTOs/Usuario/UpdateUsuarioDto.cs`

**Descripción:**
El DTO para actualizar un usuario incluye los campos `Rol`, `Estado` y `Permisos`. Cualquier usuario autenticado puede enviarse a sí mismo (o a otro usuario) un rol superior.

**Fix (Paso 5):**

```csharp
// UpdateUsuarioDto.cs — remover estos campos:
// public string? Rol { get; set; }        ← ELIMINAR
// public string? Estado { get; set; }     ← ELIMINAR
// public List<string>? Permisos { get; set; } ← ELIMINAR

// Crear DTOs separados para admin:
public class UpdateRolUsuarioDto
{
    [Required]
    public string Rol { get; set; } = string.Empty;
}

public class UpdateEstadoUsuarioDto
{
    [Required]
    public string Estado { get; set; } = string.Empty;
}
```

---

#### [C-07] CreateMovimientoCajaDto.UsuarioId viene del cliente

**Severidad:** 🔴 CRÍTICO
**Archivo:** `src/Application/DTOs/Caja/CreateMovimientoCajaDto.cs`

**Descripción:**
El campo `UsuarioId` en el DTO de creación de movimiento de caja es enviado por el cliente, permitiendo atribuir movimientos a cualquier usuario.

**Fix (Paso 6):**

```csharp
// CreateMovimientoCajaDto.cs — remover UsuarioId:
// public int UsuarioId { get; set; }  ← ELIMINAR

// CajaController.cs — extraer del JWT:
[HttpPost]
public async Task<IActionResult> CrearMovimiento([FromBody] CreateMovimientoCajaDto dto)
{
    var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    await _cajaService.CrearMovimientoAsync(dto, userId);
    return Ok();
}

// CajaService.cs — agregar parámetro:
public async Task CrearMovimientoAsync(CreateMovimientoCajaDto dto, int usuarioId)
{
    // usar usuarioId en lugar de dto.UsuarioId
}
```

---

#### [C-08] WebhooksController acepta webhooks MP sin firma cuando secret está vacío

**Severidad:** 🔴 CRÍTICO
**Archivo:** `src/Api/Controllers/WebhooksController.cs` (~línea 156)

**Descripción:**
Si `WebhookSecret` no está configurado, el controller omite la validación de firma HMAC y procesa el webhook de todas formas. Un atacante puede falsificar notificaciones de pago.

**Fix (Paso 7):**

```csharp
// WebhooksController.cs
private bool ValidarFirmaWebhook(HttpRequest request, string body)
{
    var secret = _configuration["MercadoPago:WebhookSecret"];

    // CAMBIO: fallar si no hay secret configurado
    if (string.IsNullOrEmpty(secret))
    {
        _logger.LogError("WebhookSecret no configurado. Rechazando webhook.");
        return false;  // era: return true
    }

    // ... resto de validación HMAC
}
```

---

### 2.2 Autorización y Control de Acceso

---

#### [A-01] Mayoría de controllers sin [Authorize]

**Severidad:** 🟠 ALTO
**Archivos:** `SucursalesController.cs`, `TurnosController.cs`, `ServiciosController.cs`, `ClientesController.cs`, `UsuariosController.cs`, `PagosController.cs`

**Descripción:**
Estos controllers manejan datos sensibles de negocio pero no tienen `[Authorize]` a nivel de clase. Confían en que los endpoints individuales lo tengan, pero es un patrón propenso a errores.

**Fix (Paso 4) — opción recomendada, FallbackPolicy global:**

```csharp
// Program.cs
builder.Services.AddAuthorization(options =>
{
    // Todos los endpoints requieren autenticación por defecto
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
```

Los endpoints públicos deben marcarse explícitamente:
```csharp
[AllowAnonymous]
[HttpGet("disponibilidad")]
public async Task<IActionResult> GetDisponibilidad(...) { ... }
```

**O bien, agregar [Authorize] en cada controller manualmente:**

```csharp
[ApiController]
[Route("api/sucursales")]
[Authorize]  // ← AGREGAR en cada controller
public class SucursalesController : ControllerBase { ... }
```

---

#### [A-02] IDOR en AjustesController — sucursalId sin validar vs JWT

**Severidad:** 🟠 ALTO
**Archivo:** `src/Api/Controllers/AjustesController.cs`
**Afecta:** 6 endpoints

**Descripción:**
Los endpoints reciben `sucursalId` como query parameter o route param y lo usan directamente, sin verificar que el usuario autenticado tenga acceso a esa sucursal.

**Fix (Paso 8):**

```csharp
// Helpers/AuthHelpers.cs — método de extensión
public static class ClaimsPrincipalExtensions
{
    public static int GetSucursalId(this ClaimsPrincipal user)
    {
        var claim = user.FindFirstValue("SucursalId");
        return int.TryParse(claim, out var id) ? id : 0;
    }

    public static void ValidarAccesoSucursal(this ClaimsPrincipal user, int sucursalId)
    {
        var sucursalClaim = user.GetSucursalId();
        var esSuperAdmin = user.FindFirstValue("EsSuperAdmin") == "True";

        if (!esSuperAdmin && sucursalClaim != sucursalId)
            throw new ForbiddenException("No tiene acceso a esta sucursal.");
    }
}

// AjustesController.cs — en cada action:
[HttpGet("{sucursalId}/horarios")]
public async Task<IActionResult> GetHorarios(int sucursalId)
{
    User.ValidarAccesoSucursal(sucursalId);  // ← AGREGAR
    // ...
}
```

---

#### [A-03] IDOR en SuscripcionController y OnboardingController

**Severidad:** 🟠 ALTO
**Archivos:** `src/Api/Controllers/SuscripcionController.cs`, `src/Api/Controllers/OnboardingController.cs`

**Descripción:**
Mismo patrón que A-02: IDs de empresa/sucursal recibidos del cliente sin validar contra los claims del JWT.

**Fix (Paso 9):**
Aplicar el mismo helper `ValidarAccesoSucursal` / `ValidarAccesoEmpresa` en cada action afectada.

---

#### [A-04] OAuth callback: state sin firma HMAC → CSRF

**Severidad:** 🟠 ALTO
**Archivo:** `src/Api/Controllers/MercadoPagoOAuthController.cs`

**Descripción:**
El parámetro `state` del flujo OAuth de MercadoPago se genera y verifica sin firma criptográfica. Un atacante puede construir una URL de callback con un `state` arbitrario y vincular su cuenta MP a la cuenta de otro usuario.

**Fix (Paso 10):**

```csharp
// MercadoPagoOAuthController.cs

// Al generar el state:
private string GenerarState(int sucursalId)
{
    var secret = _configuration["MercadoPago:OAuthStateSecret"];
    var payload = $"{sucursalId}:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
    var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
    var firma = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{payload}.{firma}"));
}

// Al verificar el state:
private (bool valido, int sucursalId) VerificarState(string state)
{
    try
    {
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(state));
        var partes = decoded.Split('.');
        if (partes.Length != 2) return (false, 0);

        var secret = _configuration["MercadoPago:OAuthStateSecret"];
        var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var firmaEsperada = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(partes[0])));

        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(firmaEsperada),
            Encoding.UTF8.GetBytes(partes[1])))
            return (false, 0);

        var sucursalId = int.Parse(partes[0].Split(':')[0]);
        return (true, sucursalId);
    }
    catch { return (false, 0); }
}
```

---

#### [A-05] PublicoController expone ex.Message en endpoint público

**Severidad:** 🟠 ALTO
**Archivo:** `src/Api/Controllers/PublicoController.cs`

**Descripción:**
Un bloque `catch(Exception ex)` devuelve `ex.Message` e `InnerException` en la respuesta HTTP. Esto puede revelar nombres de tablas, estructura de la DB, rutas del servidor, etc.

**Fix (Paso 11):**

```csharp
// ANTES (vulnerable):
catch (Exception ex)
{
    return StatusCode(500, new { error = ex.Message, detail = ex.InnerException?.Message });
}

// DESPUÉS (correcto):
catch (Exception ex)
{
    _logger.LogError(ex, "Error en endpoint público {Endpoint}", HttpContext.Request.Path);
    return StatusCode(500, new { error = "Ocurrió un error interno. Intente más tarde." });
}
```

---

#### [A-06] CreateTurnoDto.Origen manipulable desde cliente

**Severidad:** 🟠 ALTO
**Archivo:** `src/Application/DTOs/Turno/CreateTurnoDto.cs`

**Descripción:**
El campo `Origen` (ej: `"Web"`, `"Presencial"`) viene del cliente. Si el sistema aplica seña/pago sólo para turnos de origen `"Web"`, un cliente puede enviar `"Presencial"` para eludir el requisito.

**Fix (Paso 12):**

```csharp
// CreateTurnoDto.cs — remover Origen:
// public string? Origen { get; set; }  ← ELIMINAR

// TurnoService.cs / PublicoController.cs — asignar según contexto:
// En endpoint público (cliente web):
turno.Origen = "Web";

// En endpoint de back-office (staff):
turno.Origen = "Presencial";
```

---

### 2.3 Validación y Overposting

---

#### [M-01] Overposting en UpdateServicioDto y UpdateSucursalDto

**Severidad:** 🟡 MEDIO
**Archivos:** `src/Application/DTOs/Servicio/UpdateServicioDto.cs`, `src/Application/DTOs/Sucursal/UpdateSucursalDto.cs`

**Descripción:**
Los campos `Activo` / `Activa` permiten que cualquier usuario que pueda editar un servicio o sucursal también pueda activarlo/desactivarlo, eludiendo los controles de negocio.

**Fix (Paso 13):**

```csharp
// UpdateServicioDto.cs — remover:
// public bool? Activo { get; set; }  ← ELIMINAR

// Crear endpoint separado con autorización específica:
[HttpPatch("{id}/estado")]
[Authorize(Roles = "Admin,Owner")]
public async Task<IActionResult> CambiarEstado(int id, [FromBody] CambiarEstadoDto dto) { ... }
```

---

#### [M-02] DTOs sin validaciones de datos

**Severidad:** 🟡 MEDIO
**Archivos:** `CreateClienteDto.cs`, `UpdateClienteDto.cs`, `CreateServicioDto.cs`, `CreateTurnoDto.cs`

**Descripción:**
Ninguno de estos DTOs tiene atributos `[Required]`, `[MaxLength]`, `[Range]` ni `[EmailAddress]`. Un cliente puede enviar strings de 10MB, valores negativos o campos vacíos.

**Fix (Paso 14):**

```csharp
// CreateClienteDto.cs
public class CreateClienteDto
{
    [Required]
    [MaxLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [MaxLength(20)]
    [Phone]
    public string? Telefono { get; set; }
}

// CreateServicioDto.cs
public class CreateServicioDto
{
    [Required]
    [MaxLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [Range(0, 86400)]  // máximo 24hs en segundos
    public int DuracionMinutos { get; set; }

    [Range(0, 999999.99)]
    public decimal Precio { get; set; }
}
```

---

#### [M-03] Race condition TOCTOU en check de conflicto de horario

**Severidad:** 🟡 MEDIO
**Archivo:** `src/Application/Services/TurnoService.cs`

**Descripción:**
El check de conflicto de horario se realiza con un SELECT y luego se hace el INSERT en dos operaciones separadas. Dos requests concurrentes pueden pasar ambas el check y crear dos turnos en el mismo horario.

**Fix:**

```csharp
// TurnoService.cs — usar transacción serializable:
using var transaction = await _context.Database.BeginTransactionAsync(
    System.Data.IsolationLevel.Serializable);
try
{
    var conflicto = await _context.Turnos
        .Where(t => t.SucursalId == dto.SucursalId
                 && t.FechaHora == dto.FechaHora
                 && t.EmpleadoId == dto.EmpleadoId
                 && t.Estado != "Cancelado")
        .AnyAsync();

    if (conflicto)
        throw new ConflictException("El horario ya está ocupado.");

    // ... crear turno
    await _context.SaveChangesAsync();
    await transaction.CommitAsync();
}
catch { await transaction.RollbackAsync(); throw; }
```

---

#### [M-04] Global Query Filters incompletos

**Severidad:** 🟡 MEDIO
**Archivo:** `src/Infrastructure/Data/AppDbContext.cs`

**Descripción:**
Las entidades `Servicio` y `Caja` no tienen filtro `Activo == true`, y `MovimientoCaja` no tiene filtro por tenant (EmpresaId/SucursalId). Esto puede exponer datos de otros tenants o registros eliminados.

**Fix (Paso 15):**

```csharp
// AppDbContext.cs — en OnModelCreating:
modelBuilder.Entity<Servicio>().HasQueryFilter(s => s.Activo);
modelBuilder.Entity<Caja>().HasQueryFilter(c => c.Activa);
modelBuilder.Entity<MovimientoCaja>().HasQueryFilter(m => m.SucursalId == _currentTenantId);
```

---

#### [M-05] GenericRepository.GetByIdAsync usa FindAsync — no aplica QueryFilters

**Severidad:** 🟡 MEDIO
**Archivo:** `src/Infrastructure/Repositories/GenericRepository.cs`

**Descripción:**
`FindAsync` busca por PK y bypassa los Global Query Filters de EF Core. Esto puede devolver entidades de otros tenants o entidades eliminadas/inactivas.

**Fix:**

```csharp
// GenericRepository.cs
public async Task<T?> GetByIdAsync(int id)
{
    // ANTES (vulnerable a bypass de QueryFilters):
    // return await _context.Set<T>().FindAsync(id);

    // DESPUÉS (respeta QueryFilters):
    return await _context.Set<T>()
        .FirstOrDefaultAsync(e => EF.Property<int>(e, "Id") == id);
}
```

---

#### [M-06] Sin rate limiting en endpoints de autenticación

**Severidad:** 🟡 MEDIO
**Archivo:** `src/Api/Program.cs`

**Descripción:**
Los endpoints `/auth/login`, `/auth/forgot-password` y `/auth/register` no tienen límite de peticiones. Un atacante puede realizar fuerza bruta de contraseñas o abusar del envío de emails.

**Fix (Paso 16):**

```csharp
// Program.cs
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", config =>
    {
        config.PermitLimit = 5;
        config.Window = TimeSpan.FromMinutes(1);
        config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        config.QueueLimit = 0;
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// app.UseRateLimiter(); — agregar en el pipeline

// AuthController.cs
[HttpPost("login")]
[EnableRateLimiting("auth")]
public async Task<IActionResult> Login(...) { ... }

[HttpPost("forgot-password")]
[EnableRateLimiting("auth")]
public async Task<IActionResult> ForgotPassword(...) { ... }
```

---

#### [M-07] Longitud mínima de password inconsistente

**Severidad:** 🟡 MEDIO
**Archivos:** `RegisterDto.cs` (MinLength 6), `ResetPasswordDto.cs` (MinLength 8)

**Descripción:**
La política de contraseñas es inconsistente: registro permite 6 caracteres pero reset requiere 8.

**Fix:**

```csharp
// Crear constante compartida:
public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const string ErrorMessage = "La contraseña debe tener al menos 8 caracteres.";
}

// RegisterDto.cs:
[MinLength(PasswordPolicy.MinLength, ErrorMessage = PasswordPolicy.ErrorMessage)]
public string Password { get; set; } = string.Empty;
```

---

#### [M-08] CORS AllowAll en Development

**Severidad:** 🟡 MEDIO
**Archivo:** `src/Api/Program.cs`

**Descripción:**
La política de CORS en desarrollo permite cualquier origen, método y header. Si por error se despliega con configuración de desarrollo, expone la API a cualquier sitio.

**Fix:**

```csharp
// Program.cs — incluso en dev, limitar a orígenes conocidos:
if (app.Environment.IsDevelopment())
{
    app.UseCors(policy => policy
        .WithOrigins("http://localhost:3000", "http://localhost:5173")
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials());
}
```

---

#### [M-09] Sin global exception handler middleware

**Severidad:** 🟡 MEDIO
**Archivo:** `src/Api/Program.cs`

**Descripción:**
Sin un middleware de manejo global de excepciones, cualquier excepción no capturada puede devolver stack traces al cliente en modo Development y mensajes de error inconsistentes en Production.

**Fix:**

```csharp
// Middleware/GlobalExceptionHandlerMiddleware.cs
public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (NotFoundException ex)
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsJsonAsync(new { error = ex.Message });
        }
        catch (ForbiddenException ex)
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new { error = ex.Message });
        }
        catch (ConflictException ex)
        {
            context.Response.StatusCode = 409;
            await context.Response.WriteAsJsonAsync(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new { error = "Error interno del servidor." });
        }
    }
}

// Program.cs — registrar primero en el pipeline:
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
```

---

### 2.4 Otros (Bajos)

---

#### [B-01] JWT token en query string para SignalR

**Severidad:** 🔵 BAJO
**Descripción:**
El token JWT se transmite como `?access_token=...` en la URL de conexión SignalR. Esto lo expone en logs del servidor web, historial del browser, y headers Referer.

**Mitigación:**
Aceptable para SignalR (es el mecanismo estándar), pero asegurarse que los logs del servidor no registren query strings completos. Configurar el log level apropiado en producción.

---

#### [B-02] Sin RowVersion para concurrencia optimista

**Severidad:** 🔵 BAJO
**Entidades afectadas:** `Turno`, `Servicio`, `ConfiguracionSucursal`

**Descripción:**
Sin concurrencia optimista, dos usuarios editando el mismo registro simultáneamente pueden sobrescribirse sin advertencia.

**Fix (Paso 17):**

```csharp
// En las entidades afectadas:
[Timestamp]
public byte[] RowVersion { get; set; } = Array.Empty<byte>();
```

---

#### [B-03] TimeSpan.Parse sin try-catch en UsuarioService

**Severidad:** 🔵 BAJO
**Archivo:** `src/Application/Services/UsuarioService.cs`

**Descripción:**
Una llamada a `TimeSpan.Parse` con input proveniente del cliente puede lanzar `FormatException` no manejada.

**Fix:**

```csharp
if (!TimeSpan.TryParse(input, out var timeSpan))
    throw new ValidationException("Formato de hora inválido. Use HH:mm:ss.");
```

---

#### [B-04] Token de recuperación de contraseña en URL

**Severidad:** 🔵 BAJO
**Descripción:**
El token de reset de contraseña viaja en la URL (`/reset-password?token=xxx`), quedando en el historial del browser y potencialmente en logs.

**Mitigación:**
Considerar enviar el token como un fragmento de URL (`#token=xxx`) que no se envía al servidor, o usar formularios POST. También asegurar que los tokens expiren en 15-30 minutos.

---

#### [B-05] GET /api/turnos/by-token/{token} devuelve datos internos

**Severidad:** 🔵 BAJO
**Archivo:** `src/Api/Controllers/TurnosController.cs`
**Descripción:**
El endpoint público que permite ver un turno con token devuelve el `TurnoResponseDto` completo, incluyendo IDs internos, montos y otros datos sensibles.

**Fix:**
Crear un `TurnoPublicoResponseDto` con sólo los campos necesarios para el cliente (fecha, hora, servicio, estado, nombre del negocio).

---

## 3. Plan de Acción

Los pasos están ordenados por impacto/riesgo. Los primeros 7 son **obligatorios antes del próximo deploy a producción**.

### Fase 1 — Críticos (antes del próximo deploy)

| # | Descripción | Archivo(s) | Hallazgos |
|---|-------------|-----------|-----------|
| 1 | Mover secrets a user-secrets / env vars | `appsettings.json` | C-01 |
| 2 | Agregar [Authorize] a SuperAdminController | `SuperAdminController.cs` | C-02 |
| 3 | Hashear passwords en SuperAdminService | `SuperAdminService.cs` | C-03, C-04 |
| 4 | Corregir comparación de password en AuthService | `AuthService.cs` | C-05 |
| 5 | Remover Rol/Estado/Permisos de UpdateUsuarioDto | `UpdateUsuarioDto.cs` | C-06 |
| 6 | Extraer UsuarioId del JWT en CajaController | `CreateMovimientoCajaDto.cs`, `CajaController.cs`, `CajaService.cs` | C-07 |
| 7 | Fallar si WebhookSecret está vacío | `WebhooksController.cs` | C-08 |

### Fase 2 — Altos (dentro de la semana)

| # | Descripción | Archivo(s) | Hallazgos |
|---|-------------|-----------|-----------|
| 8 | FallbackPolicy o [Authorize] en todos los controllers | `Program.cs` o controllers | A-01 |
| 9 | Validar sucursalId vs JWT en AjustesController | `AjustesController.cs` | A-02 |
| 10 | Validar acceso en SuscripcionController/OnboardingController | `SuscripcionController.cs`, `OnboardingController.cs` | A-03 |
| 11 | Firmar state OAuth con HMAC | `MercadoPagoOAuthController.cs` | A-04 |
| 12 | Eliminar ex.Message de respuesta en PublicoController | `PublicoController.cs` | A-05 |
| 13 | Remover Origen de CreateTurnoDto | `CreateTurnoDto.cs`, `TurnoService.cs` | A-06 |

### Fase 3 — Medios (dentro de dos semanas)

| # | Descripción | Hallazgos |
|---|-------------|-----------|
| 14 | Remover Activo/Activa de DTOs de update | M-01 |
| 15 | Agregar [Required]/[MaxLength]/[Range] a DTOs | M-02 |
| 16 | Transacción serializable en check de horario | M-03 |
| 17 | Completar Global Query Filters | M-04 |
| 18 | Reemplazar FindAsync por FirstOrDefaultAsync | M-05 |
| 19 | Agregar rate limiting en /auth/* | M-06 |
| 20 | Unificar MinLength de password | M-07 |
| 21 | Restringir CORS en Development | M-08 |
| 22 | Implementar GlobalExceptionHandlerMiddleware | M-09 |

### Fase 4 — Bajos (próximo sprint)

| # | Descripción | Hallazgos |
|---|-------------|-----------|
| 23 | Suprimir query strings en logs para SignalR | B-01 |
| 24 | Agregar RowVersion a entidades críticas | B-02 |
| 25 | Reemplazar TimeSpan.Parse con TryParse | B-03 |
| 26 | Evaluar alternativa al token en URL para reset | B-04 |
| 27 | Crear TurnoPublicoResponseDto | B-05 |

---

## 4. Checklist de Verificación

### Antes de hacer merge a producción

- [ ] `appsettings.json` no contiene ningún secret real (password, API key, connection string con credenciales)
- [ ] `appsettings.Development.json` tampoco contiene secrets reales
- [ ] `dotnet user-secrets list` muestra los secrets en dev
- [ ] Variables de entorno configuradas en el servidor de producción
- [ ] `SuperAdminController` tiene `[Authorize]` a nivel clase
- [ ] Todos los métodos de `SuperAdminService` que escriben passwords usan `BCrypt.HashPassword`
- [ ] `AuthService` usa `BCrypt.Verify` para comparar passwords
- [ ] `UpdateUsuarioDto` no tiene campos `Rol`, `Estado`, ni `Permisos`
- [ ] `CreateMovimientoCajaDto` no tiene campo `UsuarioId`
- [ ] `WebhooksController` rechaza requests si `WebhookSecret` está vacío

### Verificación de build y tests

```bash
# Desde la carpeta raíz del backend:
dotnet build src/TurneroAPP.sln

# Si hay tests:
dotnet test src/TurneroAPP.sln

# Verificar que no hay secrets en el código fuente:
grep -rn "BCrypt\|password\|secret\|apikey" src/Api/appsettings*.json
```

### Verificación con Swagger

1. Abrir Swagger UI sin token
2. Intentar `GET /api/superadmin/...` → debe devolver 401
3. Intentar `PUT /api/usuarios/{id}` con body `{"Rol": "SuperAdmin"}` → debe devolver 400 (campo ignorado) o 422
4. Intentar `POST /api/webhooks/mercadopago` sin header de firma → debe devolver 400
5. Intentar `POST /api/caja/movimiento` con `UsuarioId` en body → campo debe ser ignorado

---

## 5. Notas Adicionales

### Rotación de credenciales
Si el repositorio fue alguna vez público o tuvo acceso de terceros, **todas las credenciales deben rotarse inmediatamente**, independientemente de si se corrige el código:

- JWT Secret → generar nuevo con `openssl rand -base64 64`
- Resend API key → revocar y generar nueva en el dashboard
- MercadoPago ClientSecret → revocar en el panel de MP
- Cloudinary → revocar API key en el dashboard
- Base de datos → cambiar la contraseña del usuario de la DB

### Priorización
Si el tiempo es limitado, el orden mínimo viable para no tener una vulnerabilidad crítica explotable es: **C-02 → C-05 → C-01 → C-03/C-04 → C-06 → C-07 → C-08**.

El hallazgo C-02 (SuperAdminController sin [Authorize]) es el de mayor impacto inmediato porque permite comprometer toda la plataforma sin ninguna credencial.
