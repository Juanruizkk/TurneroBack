# Fase 4 — Hallazgos Bajos (Completado)

**Fecha:** 2026-04-22
**Estado:** ✅ Completado (5/5)
**Hallazgos resueltos:** B-01, B-02, B-03, B-04, B-05

---

## Resumen de cambios

### B-01 — JWT token en query string para SignalR

**Problema:** El token JWT se transmitía como `?access_token=...` en la URL de conexión
SignalR. WebSockets no soportan headers de Authorization, por lo que este mecanismo es
necesario. Sin embargo, el token quedaba en los logs de middleware de ASP.NET Core.

**Archivo modificado:** `src/Api/Program.cs`

**Fix aplicado:**
Dentro del handler `OnMessageReceived`, una vez leído el token para autenticar al usuario,
se elimina la clave `access_token` del `QueryString`. Los middleware que corran después
del authentication middleware ya no verán el token en la URL.

```csharp
OnMessageReceived = context =>
{
    var accessToken = context.Request.Query["access_token"];
    var path        = context.HttpContext.Request.Path;
    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
    {
        context.Token = accessToken;

        // B-01: Eliminar del QueryString para que no quede en logs de ASP.NET Core
        var queryDict = QueryHelpers.ParseQuery(context.Request.QueryString.Value);
        queryDict.Remove("access_token");
        context.Request.QueryString = QueryString.Create(
            queryDict.SelectMany(kv =>
                kv.Value.Select(v => KeyValuePair.Create(kv.Key, v ?? string.Empty))));
    }
    return Task.CompletedTask;
}
```

**Limitación conocida:** El primer log de Kestrel ("Request starting GET /hubs/turnero?access_token=...")
ocurre antes de que cualquier middleware corra, por lo que aún puede aparecer en Kestrel
verbose logs. La mitigación a nivel de servidor (nginx `log_format` sin `$query_string` para
rutas `/hubs`) es una tarea de Ops fuera del scope del código.

---

### B-02 — Sin RowVersion para concurrencia optimista

**Problema:** Las entidades `Turno`, `Servicio` y `ConfiguracionSucursal` no tenían token de
concurrencia. Dos actualizaciones simultáneas podían sobrescribirse silenciosamente.

**Archivos modificados:**

| Archivo | Cambio |
|---|---|
| `src/Domain/Entities/Turno.cs` | Agregado `public byte[] RowVersion` |
| `src/Domain/Entities/Servicio.cs` | Agregado `public byte[] RowVersion` |
| `src/Domain/Entities/ConfiguracionSucursal.cs` | Agregado `public byte[] RowVersion` |
| `src/Infrastructure/Data/Configurations/TurnoConfiguration.cs` | `builder.Property(t => t.RowVersion).IsRowVersion()` |
| `src/Infrastructure/Data/Configurations/ServicioConfiguration.cs` | `builder.Property(s => s.RowVersion).IsRowVersion()` |
| `src/Infrastructure/Data/Configurations/ConfiguracionSucursalConfiguration.cs` | `builder.Property(c => c.RowVersion).IsRowVersion()` |

**Migration generada y aplicada:**
```bash
dotnet ef migrations add AddRowVersion --project src/Infrastructure --startup-project src/Api
dotnet ef database update --project src/Infrastructure --startup-project src/Api
```

**Comportamiento:** Si dos requests intentan actualizar el mismo recurso simultáneamente,
EF Core lanzará `DbUpdateConcurrencyException` en el segundo. Los servicios pueden capturar
esta excepción y devolver `409 Conflict`.

**Próximo paso recomendado (no urgente):** Agregar manejo de `DbUpdateConcurrencyException`
en `ServicioService.UpdateAsync`, `TurnoService.UpdateEstadoAsync`, y `AjustesService`.

---

### B-03 — TimeSpan.Parse sin try-catch en UsuarioService

**Problema:** `TimeSpan.Parse(d.HoraDesde)` y `TimeSpan.Parse(d.HoraHasta)` en
`ReplaceHorariosAsync` lanzaban `FormatException` si el cliente enviaba una cadena inválida
(ej. `"25:00"`, `"abc"`). Esta excepción no controlada subía hasta el global exception handler.

**Archivo modificado:** `src/Application/Services/UsuarioService.cs`

```csharp
// ANTES (lanza FormatException)
HoraDesde = TimeSpan.Parse(d.HoraDesde),
HoraHasta = TimeSpan.Parse(d.HoraHasta),

// DESPUÉS (devuelve 400 con mensaje claro)
if (!TimeSpan.TryParse(d.HoraDesde, out var horaDesde))
    throw new InvalidOperationException($"Formato de hora inválido: '{d.HoraDesde}'. Use HH:mm.");
if (!TimeSpan.TryParse(d.HoraHasta, out var horaHasta))
    throw new InvalidOperationException($"Formato de hora inválido: '{d.HoraHasta}'. Use HH:mm.");
```

**Resultado:** Input inválido devuelve `400 Bad Request` con mensaje descriptivo en lugar de
`500 Internal Server Error`.

---

### B-04 — Token de recuperación de contraseña en URL

**Estado:** ✅ Resuelto

**Problema:** El email de reset contenía un link `https://app.com/reset-password?token=xxx`.
El token en el query param quedaba en:
- Logs de acceso del servidor (nginx, IIS, Kestrel)
- Headers `Referer` cuando el usuario navega a un tercero (analytics, CDN, redes sociales)
- Historial del browser

**Archivo modificado:** `src/Application/Services/RecuperacionContrasenaService.cs`

**Fix — URL fragment (`#`) en lugar de query param (`?`):**

```csharp
// ANTES (token en query param — aparece en logs y Referer)
var link = $"{_frontendUrl}/reset-password?token={token}";

// DESPUÉS (token en fragment — nunca se envía al servidor)
var link = $"{_frontendUrl}/reset-password#token={token}";
```

**Por qué funciona:**
Los URL fragments (`#...`) son procesados exclusivamente por el browser:
- **Nunca se envían al servidor** en ningún request HTTP → no aparecen en access logs
- **Nunca se incluyen en el header `Referer`** → terceros (analytics, CDN) no los ven
- El browser no envía el fragment en la solicitud inicial al cargar la página

**Ajuste necesario en el frontend:**
```javascript
// ANTES: leía de query param
const token = new URLSearchParams(window.location.search).get('token');

// DESPUÉS: lee del fragment
const params = new URLSearchParams(window.location.hash.slice(1));
const token = params.get('token');
// Limpiar el historial del browser inmediatamente:
window.history.replaceState(null, '', window.location.pathname);
```

**Riesgo residual:** El token sigue apareciendo brevemente en el historial del browser hasta
que el frontend llama a `replaceState`. Con el approach anterior (query param) el token
quedaba permanentemente en el historial; con fragments es eliminable en el mismo momento.

---

### B-05 — GET /api/turnos/by-token/{token} devolvía datos internos

**Problema:** El endpoint público `GET /api/turnos/by-token/{token}` (accesible sin
autenticación) devolvía el `TurnoResponseDto` completo, incluyendo:
- `Id`, `SucursalId`, `ClienteId`, `ServicioId`, `ProfesionalId` — GUIDs internos
- `PrecioServicio`, `MontoSena`, `TotalCobrado` — datos financieros
- `Origen` — dato interno de negocio
- `TokenGestion` — el token ya es conocido por el cliente, no hay necesidad de re-exponerlo

**Archivos creados/modificados:**

| Archivo | Acción |
|---|---|
| `src/Application/DTOs/Turno/TurnoPublicoResponseDto.cs` | Creado — DTO mínimo público |
| `src/Application/Services/TurnoService.cs` | Agregado `GetByTokenPublicoAsync` |
| `src/Api/Controllers/TurnosController.cs` | `GetByToken` usa `GetByTokenPublicoAsync` |

**TurnoPublicoResponseDto:**
```csharp
public class TurnoPublicoResponseDto
{
    public string ClienteNombre     { get; set; }
    public string ServicioNombre    { get; set; }
    public string ProfesionalNombre { get; set; }
    public DateTime FechaHoraInicio { get; set; }
    public DateTime FechaHoraFin    { get; set; }
    public EstadoTurno Estado       { get; set; }
    public string? Observaciones    { get; set; }
}
```

**Campos excluidos:** `Id`, `SucursalId`, `ClienteId`, `ServicioId`, `ProfesionalId`,
`Origen`, `PrecioServicio`, `MontoSena`, `TotalCobrado`, `TokenGestion`.

**Resultado:** El cliente que consulta su turno con el token ve únicamente la información
necesaria para confirmar su reserva (quién, qué, cuándo y en qué estado está).

---

## Verificación rápida

```bash
# B-03: hora inválida → 400
curl -X PUT http://localhost:5000/api/usuarios/{id}/horarios \
  -H "Authorization: Bearer TOKEN" \
  -H "Content-Type: application/json" \
  -d '[{"diaSemana":1,"horaDesde":"25:00","horaHasta":"18:00","activo":true}]'
# → 400 Bad Request: "Formato de hora inválido: '25:00'. Use HH:mm."

# B-05: turno por token solo expone datos públicos
curl http://localhost:5000/api/turnos/by-token/TOKEN_GESTION
# → { "clienteNombre": "...", "servicioNombre": "...", "profesionalNombre": "...",
#      "fechaHoraInicio": "...", "fechaHoraFin": "...", "estado": "...", "observaciones": null }
# → NO contiene: id, sucursalId, clienteId, precioServicio, montoSena, etc.

# B-02: Migration aplicada — columna xmin en PostgreSQL para RowVersion
psql -h localhost -p 5433 -U postgres -d turnero \
  -c "\d turnos" | grep row_version
```

---

## Estado final — Hallazgos Bajos

| Id | Hallazgo | Estado |
|---|---|---|
| B-01 | JWT token en query string para SignalR | ✅ Resuelto (parcial — Kestrel first-hop aún lo registra) |
| B-02 | Sin RowVersion para concurrencia optimista | ✅ Resuelto (migration aplicada) |
| B-03 | TimeSpan.Parse sin try-catch | ✅ Resuelto |
| B-04 | Token de reset en URL | ✅ Resuelto (fragment #token= en lugar de ?token=) |
| B-05 | By-token devuelve datos internos | ✅ Resuelto |
