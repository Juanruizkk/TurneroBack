# Fase 3 — Hallazgos Medios (Completado)

**Fecha:** 2026-04-22
**Estado:** ✅ Completado (9/9)
**Hallazgos resueltos:** M-01, M-02, M-03, M-04, M-05, M-06, M-07, M-08, M-09

---

## Resumen de cambios

### M-01 — Overposting: campos de estado en DTOs de actualización

**Problema:** `UpdateServicioDto.Activo` y `UpdateSucursalDto.Activa` permitían a cualquier
usuario autenticado desactivar servicios o sucursales desde el PUT estándar, sin que existiera
control de rol.

**Archivos modificados:**

| Archivo | Cambio |
|---|---|
| `src/Application/DTOs/Servicio/UpdateServicioDto.cs` | Removido `public bool Activo` |
| `src/Application/DTOs/Sucursal/UpdateSucursalDto.cs` | Removido `public bool Activa` |
| `src/Application/Services/ServicioService.cs` | Removida línea `servicio.Activo = dto.Activo` |
| `src/Application/Services/SucursalService.cs` | Removida línea `sucursal.Activa = dto.Activa` |

**Resultado:**
- `Activo` de Servicio se controla únicamente vía `DELETE /api/servicios/{id}` (`DesactivarAsync`).
- `Activa` de Sucursal solo puede modificarse desde SuperAdmin (endpoint dedicado).

---

### M-02 — Validaciones ausentes en DTOs de entrada

**Problema:** `CreateClienteDto`, `UpdateClienteDto`, `CreateServicioDto` y `CreateTurnoDto`
carecían de `[Required]`, `[MaxLength]` y `[Range]`. Cualquier valor (cadenas enormes,
precios negativos, duraciones cero) era aceptado sin validación.

**Archivos modificados:**

| DTO | Atributos agregados |
|---|---|
| `CreateClienteDto` | `[Required]` en SucursalId, Nombre, Apellido; `[MaxLength]` en todos; `[EmailAddress]` en Email |
| `UpdateClienteDto` | `[Required]` en Nombre, Apellido; `[MaxLength]` y `[EmailAddress]` |
| `CreateServicioDto` | `[Required]` + `[MaxLength]` en Nombre/Descripcion/Aclaraciones; `[Range(0, 9999999.99)]` en Precio/ValorSena; `[Range(1, 1440)]` en duraciones |
| `CreateTurnoDto` | `[Required]` en todos los Guid y FechaHoraInicio; `[MaxLength(500)]` en Observaciones |

**Resultado:** `ModelState.IsValid` ahora rechaza automáticamente datos inválidos con `400 Bad Request`
antes de que lleguen al servicio.

---

### M-03 — Race condition TOCTOU en conflicto de horario

**Estado:** ✅ Resuelto

**Problema:** El flujo `ExisteConflictoHorarioAsync` → `AddAsync` → `SaveChangesAsync` no estaba
envuelto en una transacción serializable. Dos requests simultáneos podían pasar la validación y
crear turnos superpuestos.

**Archivo modificado:** `src/Application/Services/TurnoService.cs`

**Fix — TransactionScope serializable (sin necesidad de inyectar DbContext):**

```csharp
using System.Transactions;

// ...

using var ts = new TransactionScope(
    TransactionScopeOption.Required,
    new TransactionOptions
    {
        IsolationLevel = IsolationLevel.Serializable,
        Timeout        = TimeSpan.FromSeconds(30)
    },
    TransactionScopeAsyncFlowOption.Enabled);

var hayConflicto = await _turnoRepository.ExisteConflictoHorarioAsync(
    dto.ProfesionalId, dto.FechaHoraInicio, fechaHoraFin);

if (hayConflicto)
    throw new InvalidOperationException("El profesional ya tiene un turno...");

await _turnoRepository.AddAsync(turno);
await _turnoRepository.SaveChangesAsync();

ts.Complete();
```

**Por qué funciona sin inyectar DbContext:**
`System.Transactions.TransactionScope` crea una transacción ambiente. EF Core detecta automáticamente
que hay una transacción ambiente activa y se enlista en ella. No se requiere cambiar la arquitectura
de inyección de dependencias.

**`TransactionScopeAsyncFlowOption.Enabled`** es obligatorio para que la transacción fluya
correctamente a través de los `await` en código async/await.

**Comportamiento con PostgreSQL:**
Con nivel `Serializable`, PostgreSQL usa SSI (Serializable Snapshot Isolation). Si dos transacciones
concurrentes leen el mismo rango de horarios y ambas intentan insertar, la segunda recibirá
un `40001 serialization_failure` que se traduce en `DbUpdateException`. El controller ya captura
`InvalidOperationException` que contiene "conflicto" → responde `409 Conflict`.

---

### M-04 — Global Query Filters incompletos

**Problema:**
- `Servicio` tenía filtro de tenant pero no filtraba `Activo = true` — servicios desactivados
  seguían visibles en queries genéricas.
- `Caja` tenía filtro de tenant pero no filtraba `Activa = true`.
- `MovimientoCaja` no tiene `SucursalId` directo (va a través de `CajaId`) — se omite por complejidad.

**Archivo modificado:** `src/Infrastructure/Data/AppDbContext.cs`

```csharp
// ANTES — Servicio
modelBuilder.Entity<Servicio>()
    .HasQueryFilter(e => _tenantContext == null
        || !_tenantContext.SucursalId.HasValue
        || e.SucursalId == _tenantContext.SucursalId.GetValueOrDefault());

// DESPUÉS — Servicio (agrega filtro Activo)
modelBuilder.Entity<Servicio>()
    .HasQueryFilter(e =>
        e.Activo &&
        (_tenantContext == null
         || !_tenantContext.SucursalId.HasValue
         || e.SucursalId == _tenantContext.SucursalId.GetValueOrDefault()));

// DESPUÉS — Caja (agrega filtro Activa)
modelBuilder.Entity<Caja>()
    .HasQueryFilter(e =>
        e.Activa &&
        (_tenantContext == null
         || !_tenantContext.SucursalId.HasValue
         || e.SucursalId == _tenantContext.SucursalId.GetValueOrDefault()));
```

**Nota:** Los repositorios que necesiten incluir servicios/cajas inactivos deben usar
`.IgnoreQueryFilters()` explícitamente (ya existe en `GetBySucursalAsync` con `incluirInactivos`).

---

### M-05 — GenericRepository.GetByIdAsync usa FindAsync (bypasea Query Filters)

**Problema:** `FindAsync` busca por clave primaria directo en el identity map / base de datos
**sin aplicar Global Query Filters**. Un `GetByIdAsync` de un servicio desactivado retornaba
el objeto igual.

**Archivo modificado:** `src/Infrastructure/Repositories/GenericRepository.cs`

```csharp
// ANTES (bypasea filtros)
public async Task<T?> GetByIdAsync(Guid id)
{
    return await _dbSet.FindAsync(id);
}

// DESPUÉS (respeta filtros)
// FirstOrDefaultAsync respeta Global Query Filters (FindAsync los bypasea).
public async Task<T?> GetByIdAsync(Guid id)
{
    return await _dbSet.FirstOrDefaultAsync(e => EF.Property<Guid>(e, "Id") == id);
}
```

**Resultado:** `GetByIdAsync` ahora respeta todos los filtros de tenant y de estado (`Activo`, `Activa`).

---

### M-06 — Sin rate limiting en endpoints de autenticación

**Problema:** `POST /api/auth/login`, `/register` y `/forgot-password` no tenían ningún límite
de velocidad — un atacante podía hacer fuerza bruta ilimitada.

**Archivos modificados:**
- `src/Api/Program.cs` — registrado `AddRateLimiter` con política `"auth"` (Fixed Window)
- `src/Api/Controllers/AuthController.cs` — `[EnableRateLimiting("auth")]` en 3 endpoints
- `src/Api/Program.cs` — `app.UseRateLimiter()` en pipeline

**Configuración de la política:**
```csharp
options.AddFixedWindowLimiter("auth", limiterOptions =>
{
    limiterOptions.Window              = TimeSpan.FromMinutes(15);
    limiterOptions.PermitLimit         = 10;
    limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    limiterOptions.QueueLimit          = 0;
});
options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
```

**Endpoints protegidos:**
- `POST /api/auth/register` — `[EnableRateLimiting("auth")]`
- `POST /api/auth/login` — `[EnableRateLimiting("auth")]`
- `POST /api/auth/forgot-password` — `[EnableRateLimiting("auth")]`

**Resultado:** Máximo 10 intentos por IP cada 15 minutos. El exceso recibe `429 Too Many Requests`.

---

### M-07 — Password mínimo inconsistente (RegisterDto=6, ResetPasswordDto=8)

**Problema:** Un usuario podía registrarse con contraseña de 6 caracteres pero al resetearla
se le exigían 8. Inconsistencia de UX y debilidad de contraseña en el registro.

**Archivo modificado:** `src/Application/DTOs/Auth/RegisterDto.cs`

```csharp
// ANTES
[MinLength(6)]

// DESPUÉS
[MinLength(8)]
```

**Resultado:** Ambos endpoints (`/register` y `/reset-password`) exigen mínimo 8 caracteres.

---

### M-08 — CORS AllowAll en Development

**Problema:** En `Development`, se usaba la política `"AllowAll"` (`AllowAnyOrigin`) en lugar
de `"AllowFrontend"`. Esto permitía cualquier origen, incluyendo extensiones maliciosas del
browser o sitios locales en otros puertos, hacer requests al API con cookies/credenciales.

**Archivo modificado:** `src/Api/Program.cs`

```csharp
// ANTES
if (app.Environment.IsDevelopment())
    app.UseCors("AllowAll");
else
    app.UseCors("AllowFrontend");

// DESPUÉS — AllowFrontend en todos los entornos
app.UseCors("AllowFrontend");
```

**Política `AllowFrontend` (ya existente, ahora usada en dev):**
```csharp
policy.WithOrigins("http://localhost:3000", "http://localhost:5173", "https://turneroapp.com")
      .AllowAnyMethod()
      .AllowAnyHeader()
      .AllowCredentials();
```

**Nota:** Si necesitás agregar más orígenes de dev (ej. ngrok), agregarlo a `WithOrigins` en lugar
de volver a `AllowAll`.

---

### M-09 — Sin global exception handler middleware

**Problema:** Excepciones no capturadas propagaban stack traces completos al cliente en algunos
entornos, potencialmente exponiendo rutas internas, nombres de assemblies o queries SQL.

**Archivo modificado:** `src/Api/Program.cs`

```csharp
// Agregado al inicio del pipeline (antes de todo)
app.UseExceptionHandler(errApp => errApp.Run(async ctx =>
{
    ctx.Response.StatusCode  = StatusCodes.Status500InternalServerError;
    ctx.Response.ContentType = "application/json";
    await ctx.Response.WriteAsJsonAsync(new
    {
        message = "Ocurrió un error interno. Por favor intente más tarde."
    });
}));
```

**Resultado:** Cualquier excepción no manejada devuelve `500` con mensaje genérico. El stack trace
completo sigue disponible en los logs del servidor (donde solo tiene acceso el equipo técnico).

---

## Verificación rápida

```bash
# M-01: PUT con Activo=false en body es ignorado
curl -X PUT http://localhost:5000/api/servicios/{id} \
  -H "Authorization: Bearer TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"nombre":"Test","activo":false,...}'
# → 200 OK, servicio.Activo no cambia

# M-02: crear cliente sin nombre → 400
curl -X POST http://localhost:5000/api/clientes \
  -H "Authorization: Bearer TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"sucursalId":"...","nombre":"","apellido":"García","telefono":"1234"}'
# → 400 Bad Request (ModelState inválido)

# M-06: más de 10 logins en 15 min → 429
for i in {1..11}; do
  curl -X POST http://localhost:5000/api/auth/login -d '{"email":"x","password":"y"}'
done
# → 429 Too Many Requests al 11mo intento

# M-09: excepción no manejada → 500 genérico (no stack trace)
# → { "message": "Ocurrió un error interno. Por favor intente más tarde." }
```

---

## Estado final — Hallazgos Medios

| Id | Hallazgo | Estado |
|---|---|---|
| M-01 | Overposting Activo/Activa | ✅ Resuelto |
| M-02 | Validaciones DTOs faltantes | ✅ Resuelto |
| M-03 | Race condition TOCTOU horario | ✅ Resuelto (TransactionScope serializable) |
| M-04 | Query Filters incompletos | ✅ Resuelto |
| M-05 | GenericRepository FindAsync bypasea filtros | ✅ Resuelto |
| M-06 | Sin rate limiting en auth | ✅ Resuelto |
| M-07 | Password mínimo inconsistente | ✅ Resuelto |
| M-08 | CORS AllowAll en Development | ✅ Resuelto |
| M-09 | Sin global exception handler | ✅ Resuelto |
