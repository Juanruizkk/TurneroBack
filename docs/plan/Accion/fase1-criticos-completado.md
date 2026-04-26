# Fase 1 — Fixes Críticos (Completado)

**Fecha:** 2026-04-22
**Estado:** ✅ Completado
**Hallazgos resueltos:** C-01, C-02, C-03, C-04, C-05, C-06, C-07, C-08

---

## Resumen de cambios

### C-01 — Secrets hardcodeados en appsettings.json

**Archivos modificados:**
- `src/Api/appsettings.json`
- `src/Api/appsettings.Development.json`

**Qué se hizo:**
Se reemplazaron todos los valores reales de credenciales con el placeholder `REPLACE_WITH_USER_SECRET` en ambos archivos de configuración.

Los secrets reales se migraron al sistema de `dotnet user-secrets` (almacenado fuera del repositorio en `%APPDATA%\Microsoft\UserSecrets\8278d061-1783-4114-9a84-8a0fe7566bb4\secrets.json`).

**Secrets migrados:**
| Clave | Destino |
|-------|---------|
| `ConnectionStrings:DefaultConnection` (password) | user-secrets |
| `Jwt:Secret` | user-secrets |
| `SuperAdmin:Password` | user-secrets |
| `Resend:ApiToken` | user-secrets |
| `MercadoPago:WebhookSecret` | user-secrets |
| `MercadoPago:MasterAccessToken` | user-secrets |
| `MercadoPagoOAuth:ClientId` | user-secrets |
| `MercadoPagoOAuth:ClientSecret` | user-secrets |
| `Cloudinary:CloudName` | user-secrets |
| `Cloudinary:ApiKey` | user-secrets |
| `Cloudinary:ApiSecret` | user-secrets |

**Cómo ver los secrets actuales en dev:**
```bash
cd src/Api
dotnet user-secrets list
```

**Para producción** — usar variables de entorno con la convención de .NET:
```
ConnectionStrings__DefaultConnection=Host=...
Jwt__Secret=...
SuperAdmin__Password=...
```

---

### C-02 — SuperAdminController sin [Authorize]

**Archivo modificado:** `src/Api/Controllers/SuperAdminController.cs`

**Cambio:**
```csharp
// ANTES
[ApiController]
[Route("api/superadmin")]
public class SuperAdminController : ControllerBase

// DESPUÉS
[ApiController]
[Route("api/superadmin")]
[Authorize(Roles = "SuperAdmin")]   // ← AGREGADO
public class SuperAdminController : ControllerBase
```

Ahora todos los endpoints de `/api/superadmin/*` requieren un token JWT con `ClaimTypes.Role = "SuperAdmin"`. Sin token válido devuelven 401.

---

### C-03 + C-04 — Passwords sin hashear en SuperAdminService

**Archivo modificado:** `src/Application/Services/SuperAdminService.cs`

**Cambio 1 — `CrearConOwnerAsync` (línea ~85):**
```csharp
// ANTES (vulnerable)
PasswordHash = dto.OwnerPassword,

// DESPUÉS
PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.OwnerPassword),
```

**Cambio 2 — `SincronizarPasswordAsync` (línea ~166):**
```csharp
// ANTES (vulnerable)
usuario.PasswordHash = nuevoPassword;

// DESPUÉS
usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(nuevoPassword);
```

**Impacto:** Los usuarios creados/sincronizados por SuperAdmin ahora tienen sus contraseñas hasheadas correctamente con BCrypt work factor 11 (default).

---

### C-05 — AuthService comparación SA en texto plano

**Archivo modificado:** `src/Application/Services/AuthService.cs`

**Cambio:**
```csharp
// ANTES (vulnerable a timing attack)
if (dto.Password != _saPassword)
    throw new UnauthorizedAccessException("Credenciales incorrectas.");

// DESPUÉS (comparación en tiempo constante)
var passwordBytes = Encoding.UTF8.GetBytes(dto.Password);
var expectedBytes = Encoding.UTF8.GetBytes(_saPassword);
if (!CryptographicOperations.FixedTimeEquals(passwordBytes, expectedBytes))
    throw new UnauthorizedAccessException("Credenciales incorrectas.");
```

El uso de `CryptographicOperations.FixedTimeEquals` garantiza que el tiempo de comparación no varía según cuántos caracteres coinciden, eliminando la posibilidad de un timing attack para determinar la contraseña del SuperAdmin.

**Nota:** La comparación por string directo (`!=`) tiene tiempo variable — termina en el primer carácter que no coincide, lo que permite a un atacante hacer fuerza bruta midiendo tiempos de respuesta.

---

### C-06 — UpdateUsuarioDto escalación de privilegios

**Archivos modificados/creados:**
- `src/Application/DTOs/Usuario/UpdateUsuarioDto.cs` — removidos `Rol`, `Estado`, `Permisos`
- `src/Application/DTOs/Usuario/UpdateRolEstadoPermisosDto.cs` — **nuevo archivo**
- `src/Application/Services/UsuarioService.cs` — `UpdateAsync` simplificado, nuevo `UpdateRolEstadoPermisosAsync`
- `src/Api/Controllers/UsuariosController.cs` — nuevo endpoint con autorización

**UpdateUsuarioDto antes:**
```csharp
public class UpdateUsuarioDto {
    public string Nombre { get; set; }
    public string Apellido { get; set; }
    public string Email { get; set; }
    public string Telefono { get; set; }
    public RolUsuario Rol { get; set; }           // ← ELIMINADO
    public EstadoUsuario Estado { get; set; }     // ← ELIMINADO
    public List<PermisoSistema> Permisos { get; set; } // ← ELIMINADO
}
```

**UpdateUsuarioDto después:**
```csharp
public class UpdateUsuarioDto {
    public string Nombre { get; set; }
    public string Apellido { get; set; }
    public string Email { get; set; }
    public string Telefono { get; set; }
    // Rol/Estado/Permisos movidos a UpdateRolEstadoPermisosDto
}
```

**Nuevo DTO:**
```csharp
// UpdateRolEstadoPermisosDto.cs
public class UpdateRolEstadoPermisosDto {
    public RolUsuario Rol { get; set; }
    public EstadoUsuario Estado { get; set; }
    public List<PermisoSistema> Permisos { get; set; }
}
```

**Nuevo endpoint (solo Administrativo):**
```
PUT /api/usuarios/{id}/rol-estado-permisos
[Authorize(Roles = "Administrativo")]
```

**Resultado:** El endpoint `PUT /api/usuarios/{id}` ya no permite cambiar el rol o escalar privilegios. Solo actualiza nombre, apellido, email y teléfono. El cambio de rol/estado/permisos requiere un token con rol `Administrativo`.

---

### C-07 — CreateMovimientoCajaDto.UsuarioId del cliente

**Archivo modificado:** `src/Application/DTOs/Caja/CreateMovimientoCajaDto.cs`

**Cambio:**
```csharp
// ANTES
public class CreateMovimientoCajaDto {
    public Guid CajaId { get; set; }
    public Guid UsuarioId { get; set; }   // ← ELIMINADO
    public Guid? TurnoId { get; set; }
    // ...
}
```

**Nota:** `CajaController` ya usaba `RegistrarMovimientoDto` (no `CreateMovimientoCajaDto`) y extraía el `userId` del JWT correctamente. Este fix elimina el campo inseguro del DTO legacy para prevenir uso futuro incorrecto.

---

### C-08 — WebhooksController acepta sin firma cuando secret vacío

**Archivo modificado:** `src/Api/Controllers/WebhooksController.cs`

**Cambio:**
```csharp
// ANTES (inseguro — permitía webhooks no autenticados)
if (string.IsNullOrEmpty(effectiveSecret))
    return true;

// DESPUÉS (seguro — rechaza si no está configurado)
if (string.IsNullOrEmpty(effectiveSecret))
{
    _logger.LogError("WebhookSecret no configurado. Se rechaza el webhook por seguridad.");
    return false;
}
```

**Resultado:** Si `MercadoPago:WebhookSecret` no está configurado, el webhook es rechazado. Esto fuerza la configuración explícita del secret antes de que los webhooks funcionen en cualquier entorno.

---

## Estado de los user-secrets (dev local)

```
UserSecretsId: 8278d061-1783-4114-9a84-8a0fe7566bb4
Ubicación: %APPDATA%\Microsoft\UserSecrets\8278d061-...\secrets.json
```

Todos los secrets del entorno de desarrollo están configurados y el proyecto puede ejecutarse con `dotnet watch run --project src/Api` sin necesitar valores en los archivos de configuración.

---

## Verificación rápida

```bash
# Confirmar que appsettings no tiene secrets reales
grep -n "re_\|APP_USR\|BCrypt\|SuperAdmin2025" src/Api/appsettings*.json
# Debe devolver vacío

# Listar secrets configurados
cd src/Api && dotnet user-secrets list

# Build limpio
dotnet build TurneroApp.sln
```
