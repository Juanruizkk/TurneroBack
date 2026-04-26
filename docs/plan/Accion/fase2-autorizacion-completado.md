# Fase 2 — Autorización y Control de Acceso (Completado)

**Fecha:** 2026-04-22
**Estado:** ✅ Completado
**Hallazgos resueltos:** A-01, A-02, A-03, A-04, A-05, A-06

---

## Resumen de cambios

### A-01 — Controllers sin [Authorize]

**Problema:** Los controllers `SucursalesController`, `TurnosController`, `ServiciosController`, `ClientesController`, `UsuariosController` y `PagosController` no tenían `[Authorize]` a nivel de clase. Cualquier petición anónima podía acceder a datos sensibles del negocio.

**Archivos modificados:**

| Controller | Cambio |
|---|---|
| `SucursalesController` | Agregado `[Authorize]` a nivel clase |
| `ServiciosController` | Agregado `[Authorize]` a nivel clase |
| `ClientesController` | Agregado `[Authorize]` a nivel clase |
| `UsuariosController` | Agregado `[Authorize]` a nivel clase |
| `TurnosController` | Agregado `[Authorize]` + `[AllowAnonymous]` en `GetByToken` y `Create` |
| `PagosController` | Agregado `[Authorize]` + `[AllowAnonymous]` en 3 endpoints públicos |

**Endpoints que mantienen acceso anónimo (intencional):**

- `GET /api/turnos/by-token/{token}` — clientes consultan su propio turno con el token de gestión
- `POST /api/turnos` — booking público + staff autenticado (origen determinado server-side)
- `POST /api/pagos/confirmar-redirect` — callback de MP post-pago
- `POST /api/pagos/turnos/{id}/verificar-pago` — verificación de pago por el cliente
- `POST /api/pagos/turnos/{id}/iniciar-pago` — generación de link de pago para el cliente

---

### A-02 — IDOR en AjustesController

**Problema:** Los 7 endpoints de `AjustesController` recibían `sucursalId` como query param y lo usaban directamente sin validar que el usuario autenticado tenga acceso a esa sucursal.

**Archivo modificado:** `src/Api/Controllers/AjustesController.cs`

**Fix:** Se agregó el método privado `ValidarAccesoSucursal(Guid sucursalId)` y se llama al inicio de cada action:

```csharp
private IActionResult? ValidarAccesoSucursal(Guid sucursalId)
{
    var esSuperAdmin = User.FindFirstValue("esSuperAdmin") == "true";
    if (esSuperAdmin) return null; // SuperAdmin bypassa la validación

    var claimSucursalId = User.FindFirstValue("sucursalId");
    if (!Guid.TryParse(claimSucursalId, out var tokenSucursalId) || tokenSucursalId != sucursalId)
        return Forbid(); // 403 si intenta acceder a otra sucursal

    return null;
}
```

**Endpoints protegidos (7):**
- `GET /api/ajustes`
- `PUT /api/ajustes`
- `POST /api/ajustes/imagen`
- `DELETE /api/ajustes/imagen`
- `GET /api/ajustes/slug`
- `GET /api/ajustes/slug-disponible`
- `PATCH /api/ajustes/slug`

**Resultado:** Un usuario de la sucursal A que intente modificar ajustes de la sucursal B recibe `403 Forbidden`.

---

### A-03 — IDOR en SuscripcionController y OnboardingController

**Problema:** Mismo patrón que A-02 — `sucursalId` del query param sin validar contra el token JWT.

**Archivos modificados:**
- `src/Api/Controllers/SuscripcionController.cs`
- `src/Api/Controllers/OnboardingController.cs`

**Fix:** Mismo helper `ValidarAccesoSucursal` agregado a ambos controllers, llamado al inicio de cada action.

---

### A-04 — OAuth callback state sin firma HMAC → CSRF

**Problema:** El parámetro `state` del flujo OAuth de MercadoPago era simplemente el UUID de la sucursal (`sucursalId.ToString("N")`), sin firma criptográfica. Un atacante podía:
1. Obtener la URL de autorización de cualquier sucursal (el endpoint `Start` es público para auth usuarios)
2. Completar su propio flujo OAuth con MP
3. Enviar el callback con el `state` de la sucursal víctima → vincular su cuenta MP a la sucursal víctima

**Archivos modificados:**
- `src/Infrastructure/Services/MercadoPago/MercadoPagoOAuthOptions.cs` — agregada propiedad `StateSecret`
- `src/Api/Controllers/MercadoPagoOAuthController.cs` — reescrita generación y verificación del state

**Fix — Nuevo state firmado (payload + HMAC-SHA256):**

```
state = base64( "{sucursalIdN}:{unixTimestamp}.{base64(HMAC-SHA256(payload, StateSecret))}" )
```

**Generación (en `Start`):**
```csharp
private string GenerarStateFirmado(Guid sucursalId)
{
    var payload = $"{sucursalId:N}:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_oauthOptions.StateSecret));
    var firma = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{payload}.{firma}"));
}
```

**Verificación (en `Callback`):**
```csharp
private Guid? VerificarState(string state)
{
    // Decodifica base64, separa payload de firma, verifica HMAC en tiempo constante
    // Devuelve sucursalId si OK, null si firma inválida
}
```

El `Callback` ahora rechaza cualquier state cuya firma no coincida con el secret del servidor, usando `CryptographicOperations.FixedTimeEquals` para prevenir timing attacks.

**Secret agregado a user-secrets:**
```
MercadoPagoOAuth:StateSecret = TurneroApp_OAuth_State_HMAC_Secret_2025_ChangeInProd!
```
⚠️ Rotar este valor en producción.

---

### A-05 — PublicoController expone ex.Message en endpoint público

**Problema:**
```csharp
// ANTES (vulnerable)
catch (Exception ex)
{
    return StatusCode(500, new { message = ex.Message, detail = ex.InnerException?.Message });
}
```

El endpoint `GET /api/publico/{slug}/slots` devolvía el mensaje de excepción completo al cliente, potencialmente exponiendo nombres de tablas, queries SQL, rutas del servidor, etc.

**Archivo modificado:** `src/Api/Controllers/PublicoController.cs`

**Fix:**
```csharp
// DESPUÉS (seguro)
catch (Exception ex)
{
    _logger.LogError(ex, "Error al obtener slots para slug={Slug} fecha={Fecha} servicioId={ServicioId}",
        slug, fecha, servicioId);
    return StatusCode(500, new { message = "Error interno al obtener disponibilidad. Intente más tarde." });
}
```

La excepción completa se registra en los logs del servidor (donde solo tiene acceso el equipo técnico) pero el cliente recibe un mensaje genérico.

---

### A-06 — CreateTurnoDto.Origen manipulable desde cliente

**Problema:** El campo `Origen` (`Web`, `Administrativo`, `Profesional`, `Caja`, `Manual`) venía del cliente en el body del request. Un cliente podía enviar `Origen = "Administrativo"` para eludir el requisito de seña/pago que solo aplica a turnos `Web`.

**Lógica del negocio que dependía de Origen:**
- Turnos `Web` con `RequiereSena = true` → estado inicial `PendientePago`
- Turnos `Web` con modo `Manual` → estado inicial `Pendiente`
- Todos los demás → estado inicial `Confirmado`

**Archivos modificados:**
- `src/Application/DTOs/Turno/CreateTurnoDto.cs` — removido el campo `Origen`
- `src/Application/Services/TurnoService.cs` — `CreateAsync` ahora recibe `OrigenTurno origen` como parámetro explícito
- `src/Api/Controllers/TurnosController.cs` — determina el origen server-side

**Fix en CreateTurnoDto:**
```csharp
// ANTES
public OrigenTurno Origen { get; set; }  // ← cliente lo enviaba

// DESPUÉS: campo eliminado del DTO
```

**Fix en TurnoService.CreateAsync:**
```csharp
// ANTES
public async Task<TurnoResponseDto> CreateAsync(CreateTurnoDto dto)

// DESPUÉS
public async Task<TurnoResponseDto> CreateAsync(CreateTurnoDto dto, OrigenTurno origen = OrigenTurno.Web)
```

**Fix en TurnosController.Create:**
```csharp
// Determinación server-side según contexto de autenticación:
var origen = User.Identity?.IsAuthenticated == true
    ? OrigenTurno.Administrativo   // staff autenticado
    : OrigenTurno.Web;             // cliente anónimo (booking online)

var turno = await _turnoService.CreateAsync(dto, origen);
```

**Resultado:** Un cliente anónimo que intente eludir la seña enviando cualquier campo `Origen` en el body lo verá ignorado — el DTO ya no lo acepta, y el origen siempre se determina en el servidor.

---

## Verificación rápida

```bash
# Sin token: no puede acceder a endpoints de staff
curl -X GET http://localhost:5000/api/sucursales
# → 401 Unauthorized

# Sin token: puede acceder al booking
curl -X GET "http://localhost:5000/api/turnos/by-token/TOKEN_GESTION"
# → 200 OK

# Con token de sucursal A: no puede ver ajustes de sucursal B
curl -H "Authorization: Bearer TOKEN_SUCURSAL_A" \
     "http://localhost:5000/api/ajustes?sucursalId=GUID_SUCURSAL_B"
# → 403 Forbidden

# OAuth state HMAC: callback con state forjado falla
curl "http://localhost:5000/api/mercadopago/oauth/callback?code=x&state=GUID_PLANO"
# → 400 Bad Request "State inválido o firma HMAC incorrecta"
```
