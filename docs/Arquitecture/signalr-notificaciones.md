# SignalR — Notificaciones en Tiempo Real

## Índice

1. [Resumen](#1-resumen)
2. [Arquitectura](#2-arquitectura)
3. [Grupos y routing](#3-grupos-y-routing)
4. [Eventos emitidos](#4-eventos-emitidos)
5. [Autenticación](#5-autenticación)
6. [Archivos involucrados](#6-archivos-involucrados)
7. [Frontend — hook useSignalR](#7-frontend--hook-usesignalr)
8. [Frontend — integración en AdminLayout](#8-frontend--integración-en-adminlayout)
9. [Cómo testear manualmente](#9-cómo-testear-manualmente)
10. [Decisiones de diseño](#10-decisiones-de-diseño)

---

## 1. Resumen

Cuando se crea un nuevo turno o se cancela uno existente, el sistema envía una notificación push en tiempo real a todos los usuarios conectados que deben saberlo:

- El **profesional asignado** recibe sus propios turnos.
- Los **administradores y cajeros** de la sucursal reciben todos los turnos de su sucursal.
- El mecanismo es **no-crítico**: si SignalR falla, la operación principal (crear/cancelar turno) no se interrumpe.

---

## 2. Arquitectura

```
TurnoService (Application)
  │  llama a ITurnoNotificacionService
  ▼
TurnoNotificacionService (Api)
  │  usa IHubContext<TurneroHub>
  ▼
TurneroHub  →  grupo "profesional-{id}"
            →  grupo "admins-{sucursalId}"
                        │
                        ▼
                 Clientes conectados
                 (frontend via @microsoft/signalr)
```

**Capa de cada componente:**

| Componente | Capa | Motivo |
|---|---|---|
| `ITurnoNotificacionService` | Application | Permite a `TurnoService` llamarla sin depender de SignalR |
| `TurnoNotificacionService` | Api | Tiene acceso a `IHubContext<TurneroHub>` (paquete SignalR) |
| `TurneroHub` | Api | Es un Hub de ASP.NET Core |
| `useSignalR` | Frontend | Hook React que encapsula la conexión |

---

## 3. Grupos y routing

Al conectarse, `TurneroHub.OnConnectedAsync` lee los claims del JWT y une al cliente al grupo correspondiente:

| Rol | Grupo | Recibe |
|---|---|---|
| `Profesional` | `profesional-{userId}` | Solo los turnos donde él es el profesional asignado |
| `Administrativo` | `admins-{sucursalId}` | Todos los turnos de la sucursal |
| `Caja` | `admins-{sucursalId}` | Todos los turnos de la sucursal |
| Owner (`esOwner = true`) | `admins-{sucursalId}` | Todos los turnos de la sucursal |
| SuperAdmin | `superadmins` | (actualmente no recibe eventos de turnos — se puede extender) |

**Aislamiento multi-sucursal:** los grupos incluyen el `sucursalId`, por lo que un admin de la Sucursal A nunca recibe notificaciones de la Sucursal B.

---

## 4. Eventos emitidos

### `NuevoTurno`

Disparado en `TurnoService.CreateAsync` luego de persistir el turno y enviar el email.

**Destinos:**
- `profesional-{turno.ProfesionalId}`
- `admins-{turno.SucursalId}`

**Payload:** objeto `TurnoResponseDto` completo serializado como JSON camelCase:

```json
{
  "id": "3fa85f64-...",
  "sucursalId": "fc6e0443-...",
  "clienteId": "...",
  "clienteNombre": "Juan Pérez",
  "clienteTelefono": "1134567890",
  "servicioId": "...",
  "servicioNombre": "Corte de cabello",
  "profesionalId": "...",
  "profesionalNombre": "Carlos García",
  "fechaHoraInicio": "2026-03-20T10:00:00",
  "fechaHoraFin": "2026-03-20T10:30:00",
  "estado": "Confirmado",
  "origen": "Web",
  "observaciones": null,
  "tokenGestion": "abc123...",
  "precioServicio": 2500.00,
  "montoSena": null,
  "totalCobrado": 0.00
}
```

### `TurnoCancelado`

Disparado en `TurnoService.UpdateEstadoAsync` cuando el nuevo estado es `CanceladoStaff` o `CanceladoCliente`.

**Destinos:** ídem `NuevoTurno`.

**Payload:** mismo formato que `NuevoTurno`, con `"estado": "CanceladoStaff"` o `"CanceladoCliente"`.

---

## 5. Autenticación

Los WebSockets no soportan el header `Authorization`, por lo que el token JWT viaja en el **query string**:

```
GET ws://localhost:5000/hubs/turnero?access_token=eyJhbG...
```

En `Program.cs`, el evento `OnMessageReceived` del `JwtBearerEvents` lee ese parámetro cuando la ruta empieza con `/hubs`:

```csharp
options.Events = new JwtBearerEvents
{
    OnMessageReceived = context =>
    {
        var accessToken = context.Request.Query["access_token"];
        var path = context.HttpContext.Request.Path;
        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            context.Token = accessToken;
        return Task.CompletedTask;
    }
};
```

El hub tiene `[Authorize]`, por lo que conexiones sin token válido son rechazadas con 401.

---

## 6. Archivos involucrados

### Backend — nuevos

| Archivo | Descripción |
|---|---|
| `src/Api/Hubs/TurneroHub.cs` | Hub SignalR. `OnConnectedAsync` asigna grupos según rol/claims |
| `src/Application/Interfaces/ITurnoNotificacionService.cs` | Interfaz `NotificarNuevoTurnoAsync` + `NotificarCancelacionAsync` |
| `src/Api/Services/TurnoNotificacionService.cs` | Implementación con `IHubContext<TurneroHub>` |

### Backend — modificados

| Archivo | Cambio |
|---|---|
| `src/Application/Services/TurnoService.cs` | + campo `ITurnoNotificacionService? _notificaciones` (opcional). Llama a la interfaz en `CreateAsync` y `UpdateEstadoAsync` |
| `src/Api/Program.cs` | + `AddSignalR()`, + `ITurnoNotificacionService` registration, JWT `OnMessageReceived`, factory de `TurnoService` actualizada, `app.MapHub<TurneroHub>("/hubs/turnero")` |

### Frontend — nuevos

| Archivo | Descripción |
|---|---|
| `src/hooks/useSignalR.ts` | Hook que crea y gestiona `HubConnection`. Acepta `token`, `onNuevoTurno`, `onTurnoCancelado` |

### Frontend — modificados

| Archivo | Cambio |
|---|---|
| `src/components/admin/AdminLayout.tsx` | Importa `useSignalR`, mantiene estado de notificaciones, muestra badge en la campanita, dropdown de notificaciones |

---

## 7. Frontend — hook `useSignalR`

```typescript
// src/hooks/useSignalR.ts
useSignalR({
  token: user?.token,               // JWT del usuario logueado
  onNuevoTurno: (turno) => { ... }, // callback cuando llega NuevoTurno
  onTurnoCancelado: (turno) => { ... }, // callback cuando llega TurnoCancelado
})
```

**Comportamiento:**
- Se conecta cuando `token` tiene valor; se desconecta si el componente se desmonta.
- Reconexión automática (`withAutomaticReconnect()`).
- Los callbacks son refs internos: cambiar `onNuevoTurno`/`onTurnoCancelado` en cada render **no** provoca reconexión.
- La URL del hub se deriva de `VITE_API_URL` quitando el sufijo `/api`:
  - `http://localhost:5000/api` → `http://localhost:5000/hubs/turnero`

**URL del hub:** `{VITE_API_URL sin /api}/hubs/turnero`

---

## 8. Frontend — integración en AdminLayout

El `AdminLayout` conecta el hook y gestiona un array de `Notificacion`:

```typescript
interface Notificacion {
  id: string;
  tipo: 'nuevo' | 'cancelado';
  mensaje: string;    // "Nuevo turno: Juan Pérez — Corte de cabello"
  hora: string;       // "10:32"
  leida: boolean;
}
```

**Reglas visuales:**
- Badge rojo en la campanita muestra conteo de no leídas (máximo "9+").
- Al abrir el dropdown, todas pasan a `leida: true` y el badge desaparece.
- Punto verde = turno nuevo. Punto rojo = turno cancelado.
- Máximo 50 notificaciones almacenadas en memoria (las más viejas se descartan automáticamente).
- Botón "Limpiar todo" vacía la lista.
- Las notificaciones son **efímeras**: no se persisten en localStorage ni en la BD — se pierden al recargar la página.

---

## 9. Cómo testear manualmente

### Prerrequisitos

```bash
# Terminal 1 — API con hot reload
cd backend
dotnet watch run --project src/Api

# Terminal 2 — Frontend
cd frontend/turnero-ui
npm run dev
```

Verificar que en los logs de la API aparezca:
```
info: Microsoft.AspNetCore.Routing.EndpointMiddleware[...] Mapped "/hubs/turnero" to hub "TurneroHub"
```

---

### Test A — Notificación de nuevo turno

**Escenario:** Un cliente reserva online y un administrador conectado recibe la notificación.

1. Abrí dos ventanas del navegador:
   - **Ventana 1:** Login como `Administrativo` o `Owner` → `/admin/agenda`
   - **Ventana 2:** Abrir `http://localhost:5173/reservar` (sin login)

2. En Ventana 1, abrí DevTools → Network → filtrar por `WebSocket` → deberías ver una conexión activa a `ws://localhost:5000/hubs/turnero?access_token=...`

3. En Ventana 2, completá el flujo de reserva hasta confirmar el turno.

4. **Resultado esperado en Ventana 1:** La campanita muestra un badge "1". Al hacer click:
   - Aparece la notificación "Nuevo turno: [Nombre cliente] — [Servicio]"
   - Badge desaparece al abrir el dropdown.

---

### Test B — Notificación de cancelación

1. Loguear como `Administrativo` en una ventana, ir a `/admin/turnos`.
2. En otra pestaña (o Swagger), cancelar un turno:

```
PATCH /api/turnos/{id}
Authorization: Bearer {token}
Content-Type: application/json

{ "estado": "CanceladoStaff" }
```

3. **Resultado esperado:** campanita muestra badge "1". Notificación con punto rojo: "Turno cancelado: [Cliente] — [Servicio]".

---

### Test C — Profesional recibe solo sus turnos

1. Loguear como `Profesional` en una ventana.
2. Crear un turno asignado **a ese profesional** (desde Swagger o desde el admin).
3. **Resultado esperado:** el profesional recibe la notificación.
4. Crear un turno asignado **a otro profesional**.
5. **Resultado esperado:** el profesional de la ventana 1 **no** recibe nada.

---

### Test D — Verificar grupos en DevTools

En DevTools → Network → WebSocket → seleccionar la conexión → tab "Messages":

Al conectarse, el servidor ejecuta `Groups.AddToGroupAsync`. Podés loguear el grupo en el hub temporalmente para depuración (no es necesario en producción):

```csharp
// En TurneroHub.OnConnectedAsync (solo para debug)
Console.WriteLine($"[SignalR] {Context.ConnectionId} → grupo: profesional-{userId} / admins-{sucursalId}");
```

---

### Test E — Reconexión automática

1. Detener la API (`Ctrl+C`).
2. En el frontend, la campanita no debería crashear (SignalR reintenta internamente).
3. Volver a levantar la API.
4. Crear un turno → el frontend debería reconectarse y empezar a recibir eventos sin recargar la página.

---

### Test F — Verificar que el hub requiere auth

```bash
# Sin token → debe retornar 401
curl -i http://localhost:5000/hubs/turnero/negotiate \
  -X POST \
  -H "Content-Type: application/json"
```

```bash
# Con token válido → retorna JSON con connectionToken y availableTransports
curl -i http://localhost:5000/hubs/turnero/negotiate \
  -X POST \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {tu_token}"
```

---

## 10. Decisiones de diseño

| Decisión | Elección | Motivo |
|---|---|---|
| Ubicación de `ITurnoNotificacionService` | Application layer | `TurnoService` (Application) puede referenciarla sin depender de SignalR ni de la capa Api |
| Ubicación de `TurnoNotificacionService` | Api layer | Necesita `IHubContext<TurneroHub>` que es un tipo de `Microsoft.AspNetCore.SignalR` |
| `notificaciones` como parámetro opcional en `TurnoService` | Sí (`= null`) | No rompe el constructor existente; el factory en `Program.cs` lo pasa explícitamente |
| SignalR es no-crítico | Sí — `try { } catch { }` | Un fallo de SignalR no puede cancelar la creación de un turno |
| Token JWT via query string | `?access_token=...` | Los WebSockets no soportan headers custom durante el handshake HTTP |
| Profesional en grupo propio, no en admins | Sí | Un profesional solo debe ver sus turnos, no los del resto del equipo |
| Notificaciones efímeras (solo en memoria) | Sí | Suficiente para alertas operativas en tiempo real; auditoría formal está en la BD |
| Max 50 notificaciones en memoria | Sí | Evita memory leaks en sesiones largas |
