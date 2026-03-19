# Tests unitarios — Notificaciones SignalR

## Descripción general

Suite de tests que verifica el comportamiento de las notificaciones en tiempo real basadas en SignalR. Cubre tres componentes:

1. **`TurnoServiceSignalRTests`** — Cuándo y cómo `TurnoService` dispara notificaciones al crear y cancelar turnos.
2. **`PagoTurnoServiceSignalRTests`** — Cuándo y cómo `PagoTurnoService` dispara notificaciones al confirmar un pago de MercadoPago.
3. **`TurnoNotificacionServiceTests`** — Que `TurnoNotificacionService` enruta los mensajes SignalR a los grupos correctos del hub.

**Proyecto:** `tests/TurneroApp.Tests/`
**Framework:** xUnit 2.6 + Moq 4.20 + FluentAssertions 6.12
**Tests nuevos:** 23 tests en 3 archivos nuevos

---

## Estructura de archivos

```
tests/
├── docs/
│   └── SignalR.tests.md                       ← este archivo
└── TurneroApp.Tests/
    └── Services/
        ├── TurnoServiceSignalRTests.cs        (9 tests)
        ├── PagoTurnoServiceSignalRTests.cs    (5 tests)
        └── TurnoNotificacionServiceTests.cs   (7 tests)
```

---

## Cómo ejecutar

```bash
# Todos los tests del proyecto
cd backend
dotnet test tests/TurneroApp.Tests/TurneroApp.Tests.csproj

# Solo los tests de SignalR y notificaciones
dotnet test tests/TurneroApp.Tests/TurneroApp.Tests.csproj \
  --filter "FullyQualifiedName~SignalR|FullyQualifiedName~TurnoNotificacion"
```

> **Nota:** Para ejecutar los tests es necesario detener `dotnet watch` primero
> (el proceso bloquea el DLL de `TurneroApp.Api`).
> Podés usar `dotnet test --no-build` si el proyecto ya compiló previamente.

---

## Reglas de negocio testeadas

### 1. Notificación de nuevo turno (CreateAsync)

El turno puede nacer en tres estados distintos, y solo uno de ellos debe disparar la notificación inmediatamente:

| Estado inicial | Condición | ¿Notifica? |
|---|---|---|
| `Confirmado` | Origen ≠ Web, o Web + modo Automático sin seña | ✅ Sí |
| `PendientePago` | Origen = Web + servicio con seña | ❌ No (espera webhook de MercadoPago) |
| `Pendiente` | Origen = Web + modo de confirmación Manual | ❌ No (espera confirmación del staff) |

**Razón:** Un turno en `PendientePago` puede nunca pagarse. Notificar en ese momento ensuciaría el calendario con turnos que podrían expirar. La notificación se dispara recién cuando el pago es confirmado por el webhook.

### 2. Notificación de cancelación (UpdateEstadoAsync)

Solo los estados de cancelación disparan `NotificarCancelacionAsync`:

| Estado nuevo | ¿Notifica cancelación? |
|---|---|
| `CanceladoStaff` | ✅ Sí |
| `CanceladoCliente` | ✅ Sí |
| `Completado` | ❌ No |
| `NoShow` | ❌ No |
| `Confirmado` | ❌ No |
| `Reprogramado` | ❌ No |

### 3. Notificación post-pago (ProcesarNotificacionPagoAsync)

Cuando MercadoPago llama al webhook:

| Status de MP | ¿Notifica? | Qué hace |
|---|---|---|
| `"approved"` | ✅ Sí — `NuevoTurno` | Confirma el turno + registra movimiento + envía email + notifica SignalR |
| `"rejected"` | ❌ No | Marca turno como `PagoRechazado` |
| `"cancelled"` | ❌ No | Marca turno como `PagoRechazado` |
| `"pending"` | ❌ No | No modifica el turno |

### 4. Enrutamiento en el hub (TurnoNotificacionService)

Cada evento se envía a dos grupos simultáneamente:

| Evento | Grupos destino |
|---|---|
| `NuevoTurno` | `profesional-{ProfesionalId}` + `admins-{SucursalId}` |
| `TurnoCancelado` | `profesional-{ProfesionalId}` + `admins-{SucursalId}` |

**Aislamiento multi-sucursal:** el `SucursalId` en el nombre del grupo garantiza que admins de una sucursal no reciben eventos de otras.

### 5. Resiliencia (no-crítico)

SignalR es **no-crítico** en toda la arquitectura. Un fallo del hub nunca debe:
- Interrumpir la creación de un turno
- Revertir la confirmación de un pago
- Impedir que la cancelación quede persistida en la BD

Los tres servicios absorben las excepciones de SignalR en bloques `try { } catch { }`.

---

## Detalle de tests

### `TurnoServiceSignalRTests.cs` — 9 tests

| Test | Qué verifica |
|---|---|
| `Create_EstadoConfirmado_DisparaNotificacionNuevoTurno` | Origen Administrativo → Confirmado → `NotificarNuevoTurnoAsync` llamado 1 vez |
| `Create_EstadoPendientePago_NoDisparaNotificacion` | Web + seña → `PendientePago` → no notifica |
| `Create_EstadoPendiente_ModoManual_NoDisparaNotificacion` | Web + modo Manual → `Pendiente` → no notifica |
| `Create_SignalRFalla_NoLanzaException` | Excepción en hub → turno sigue creándose sin error |
| `Create_NotificacionPayload_ContieneEstadoConfirmado` | El DTO capturado tiene `Estado = Confirmado` y `TotalCobrado = 0` |
| `Update_Cancelacion_DisparaNotificacionCancelacion` | `CanceladoStaff` y `CanceladoCliente` → `NotificarCancelacionAsync` 1 vez (Theory × 2) |
| `Update_EstadoNoCancelacion_NoDisparaNotificacion` | Completado/NoShow/Confirmado/Reprogramado → no notifica (Theory × 4) |
| `Update_CancelacionSignalRFalla_NoLanzaException` | Excepción en hub durante cancelación → no propaga |

### `PagoTurnoServiceSignalRTests.cs` — 5 tests

| Test | Qué verifica |
|---|---|
| `Procesar_PagoAprobado_DisparaNotificacionNuevoTurno` | Webhook "approved" → `NotificarNuevoTurnoAsync` llamado 1 vez |
| `Procesar_PagoAprobado_NotificacionPayload_EstadoEsConfirmado` | DTO tiene `Estado = Confirmado`, `TotalCobrado = monto pagado`, `SucursalId` y `ProfesionalId` correctos |
| `Procesar_PagoRechazadoOCancelado_NoDisparaNotificacion` | "rejected" y "cancelled" → no notifica (Theory × 2) |
| `Procesar_PagoAprobado_SignalRFalla_NoPropagarExcepcion` | Hub falla → turno sigue en `Confirmado`, sin excepción propagada |

### `TurnoNotificacionServiceTests.cs` — 7 tests

Mock de la cadena `IHubContext<TurneroHub> → IHubClients → IClientProxy.SendCoreAsync`.

| Test | Qué verifica |
|---|---|
| `NotificarNuevoTurno_EnviaAlGrupoProfesional` | `IHubClients.Group($"profesional-{ProfesionalId}")` llamado 1 vez |
| `NotificarNuevoTurno_EnviaAlGrupoAdmins` | `IHubClients.Group($"admins-{SucursalId}")` llamado 1 vez |
| `NotificarNuevoTurno_UsaMetodoNuevoTurno` | `SendCoreAsync("NuevoTurno", ...)` llamado exactamente 2 veces (prof + admins) |
| `NotificarCancelacion_EnviaAlGrupoProfesional` | `Group($"profesional-{...}")` → 1 vez |
| `NotificarCancelacion_EnviaAlGrupoAdmins` | `Group($"admins-{...}")` → 1 vez |
| `NotificarCancelacion_UsaMetodoTurnoCancelado` | `SendCoreAsync("TurnoCancelado", ...)` llamado exactamente 2 veces |

---

## Patrón de mocking — IHubContext

Para testear `TurnoNotificacionService` sin un servidor SignalR real se mockea la cadena completa:

```csharp
var mockHub     = new Mock<IHubContext<TurneroHub>>();
var mockClients = new Mock<IHubClients>();
var mockProxy   = new Mock<IClientProxy>();

mockHub.Setup(h => h.Clients).Returns(mockClients.Object);
mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockProxy.Object);
mockProxy
    .Setup(p => p.SendCoreAsync(
        It.IsAny<string>(),
        It.IsAny<object?[]>(),
        It.IsAny<CancellationToken>()))
    .Returns(Task.CompletedTask);
```

> `SendAsync("NuevoTurno", turno)` es un método de extensión que internamente llama a
> `SendCoreAsync("NuevoTurno", new object?[] { turno }, cancellationToken)`.
> Por eso se mockea `SendCoreAsync` (método real de la interfaz) en lugar de `SendAsync`.

Para verificar los grupos exactos:

```csharp
mockClients.Verify(c => c.Group($"profesional-{dto.ProfesionalId}"), Times.Once);
mockClients.Verify(c => c.Group($"admins-{dto.SucursalId}"),         Times.Once);
```

---

## Fix adicional — TurnoPagoExpirationService

Al correr los tests por primera vez se detectaron 3 tests preexistentes fallando en
`TurnoPagoExpirationServiceTests.cs` porque `ExpirarTurnosAbandonadosAsync()` no tenía
un `try/catch` propio (los tests esperaban que absorbiera errores de repositorio).

**Fix aplicado** en `src/Api/Services/TurnoPagoExpirationService.cs`:
el cuerpo de `ExpirarTurnosAbandonadosAsync()` fue envuelto en un `try/catch` manteniendo el
`using var scope` fuera del bloque para garantizar que el scope siempre se libere.

```
Antes:  108 tests — 105 ✅ 3 ❌ (TurnoPagoExpirationServiceTests)
Después: 108 tests — 108 ✅ 0 ❌
```
