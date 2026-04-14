# Tests unitarios — PagoTurnoService y TurnoPagoExpirationService

## Descripción general

Tests que verifican el flujo completo de pagos con MercadoPago: generación del link de pago, confirmación vía webhook, rechazo/cancelación, checkout abandonado (pestaña cerrada sin pagar) y casos borde de idempotencia y manejo de errores.

Todos los tests son completamente aislados: sin base de datos, sin llamadas reales a MP, sin SMTP.

**Proyecto:** `tests/TurneroApp.Tests/`
**Archivos de tests:**
- `tests/TurneroApp.Tests/Services/PagoTurnoServiceTests.cs`
- `tests/TurneroApp.Tests/Services/TurnoPagoExpirationServiceTests.cs`
**Framework:** xUnit 2.6 + Moq 4.20 + FluentAssertions 6.12

---

## Estructura de archivos

```
tests/
├── docs/
│   ├── TurnoService.tests.md
│   ├── TurnoAutoNoShowService.tests.md
│   └── PagoTurnoService.tests.md          ← este archivo
└── TurneroApp.Tests/
    ├── Helpers/
    │   └── TurnoTestFactory.cs            ← +FechaCreacion en CrearTurnoConRelaciones
    └── Services/
        ├── TurnoServiceCreateTests.cs     ← +3 tests de PendientePago y FechaCreacion
        ├── PagoTurnoServiceTests.cs       ← nueva suite (16 tests)
        └── TurnoPagoExpirationServiceTests.cs ← nueva suite (11 tests)
```

---

## Cómo ejecutar

```bash
# Detener dotnet watch run (Ctrl+C) — necesario en Windows por file lock

cd "C:/JUAN RUIZ/TurneroAPP/backend"

# Solo PagoTurnoService
dotnet test tests/TurneroApp.Tests/TurneroApp.Tests.csproj \
  --filter "PagoTurnoServiceTests" --verbosity normal

# Solo TurnoPagoExpirationService
dotnet test tests/TurneroApp.Tests/TurneroApp.Tests.csproj \
  --filter "TurnoPagoExpirationServiceTests" --verbosity normal

# Todo el proyecto de tests
dotnet test tests/TurneroApp.Tests/TurneroApp.Tests.csproj --verbosity normal
```

Resultado esperado: **83 passed, 0 failed**
- 18 anteriores (`TurnoServiceCreate`) → ahora 21 (+3 nuevos)
- 51 existentes totales → ahora 83 (+3 TurnoServiceCreate, +16 PagoTurnoService, +11 TurnoPagoExpiration, +2 TurnoAutoNoShow ≈ total exacto puede variar según recuento previo)

---

## Mocks en `PagoTurnoServiceTests`

| Mock | Tipo | Qué se mockea |
|---|---|---|
| `ITurnoRepository` | Repositorio | `GetByIdConRelacionesAsync`, `Update`, `SaveChangesAsync` |
| `ICajaRepository` | Repositorio | `GetBySucursalAsync`, `RegistrarMovimientoTransaccionalAsync` |
| `ISucursalRepository` | Repositorio | `GetByIdAsync` (para nombre de sucursal en el email) |
| `IMercadoPagoGateway` | Gateway externo | `CrearPreferenciaAsync`, `ObtenerDetallesPagoAsync` |
| `IEmailService` | Servicio externo | `EnviarConfirmacionTurnoAsync` |
| `ILogger<PagoTurnoService>` | Logging | Sin verificaciones |

---

## Tests: `PagoTurnoServiceTests` (16 tests)

### GenerarLinkDePagoAsync

| Test | Qué verifica |
|---|---|
| `GenerarLink_TurnoNoEncontrado_LanzaException` | Si el turno no existe, lanza `InvalidOperationException` |
| `GenerarLink_TurnoNoEsPendientePago_LanzaException` | Si el turno no está en `PendientePago`, lanza con mensaje descriptivo |
| `GenerarLink_YaTienePreferenceId_RetornaExistenteSinLlamarAlGateway` | Idempotencia: reutiliza el `preferenceId` sin crear uno nuevo en MP |
| `GenerarLink_SinPreferenceId_LlamaAlGatewayYPersiste` | Crea preferencia, la guarda en el turno y persiste |
| `GenerarLink_UsaMontoSena_CuandoMontoSenaEsMayorACero` | El monto pasado a MP es el de la seña, no el precio completo |
| `GenerarLink_UsaPrecioServicio_CuandoMontoSenaEsNulo` | Sin seña configurada, se cobra el precio completo |

### ProcesarNotificacionPagoAsync — casos de control

| Test | Qué verifica |
|---|---|
| `Procesar_ExternalReferenceInvalido_NoModificaNada` | Un `ExternalReference` que no es Guid válido es ignorado silenciosamente |
| `Procesar_TurnoNoEncontrado_NoLanzaExceptionNiModificaNada` | Turno eliminado / referencia inválida → sin excepción, sin persistencia |
| `Procesar_StatusPending_NoModificaTurno` | Status `"pending"` no cambia el estado del turno |

### ProcesarNotificacionPagoAsync — pago rechazado / cancelado

| Test | Qué verifica |
|---|---|
| `Procesar_PagoRechazadoOCancelado_MarcaTurnoComoPagoRechazado` (Theory: `"rejected"`, `"cancelled"`) | Ambos status marcan el turno como `PagoRechazado`, liberando el slot |
| `Procesar_PagoRechazado_TurnoYaEnOtroEstado_NoModifica` | No sobrescribe si el turno ya estaba en otro estado |

### ProcesarNotificacionPagoAsync — pago aprobado (checkout completado)

| Test | Qué verifica |
|---|---|
| `Procesar_PagoAprobado_TurnoYaConfirmado_Idempotencia` | Webhook duplicado → no duplica el movimiento de caja |
| `Procesar_PagoAprobado_ConfirmaTurnoYActualizaTotalCobrado` | `Estado = Confirmado`, `TotalCobrado += monto` |
| `Procesar_PagoAprobado_RegistraMovimientoEnCaja` | Crea `MovimientoCaja` con `MedioPago=MercadoPago`, `UsuarioId=null` |
| `Procesar_PagoAprobado_SinCaja_ConfirmaTurnoSinMovimiento` | Sin Caja configurada: turno se confirma igual (no queda atascado) |
| `Procesar_PagoAprobado_ClienteConEmail_EnviaEmailConfirmacion` | Envía email al cliente tras la confirmación |
| `Procesar_PagoAprobado_ClienteSinEmail_NoEnviaEmail` | Sin email del cliente → no llama al servicio de email |
| `Procesar_PagoAprobado_EmailFalla_NoPropagarExcepcion` | Fallo de SMTP no revierte la confirmación del turno |

---

## Tests: `TurnoPagoExpirationServiceTests` (11 tests)

### Sin turnos vencidos

| Test | Qué verifica |
|---|---|
| `Expirar_SinTurnosVencidos_NoLlamaAUpdateRange` | Lista vacía → sin `UpdateRange` |
| `Expirar_SinTurnosVencidos_NoLlamaASaveChanges` | Lista vacía → sin `SaveChangesAsync` |

### Estado de turnos

| Test | Qué verifica |
|---|---|
| `Expirar_ConTurnosVencidos_CambiaEstadoAPagoRechazado` | Turnos `PendientePago` vencidos pasan a `PagoRechazado` |

### Llamadas al repositorio

| Test | Qué verifica |
|---|---|
| `Expirar_ConTurnosVencidos_LlamaUpdateRangeUnaVez` | Batch único de actualización |
| `Expirar_ConTurnosVencidos_LlamaSaveChangesUnaVez` | Una sola persistencia por ciclo |

### Fecha límite

| Test | Qué verifica |
|---|---|
| `Expirar_FecheLimite_EsAnteriorA30Minutos` | La fecha límite es `DateTime.Now - 30 min` (±1 min de tolerancia) |

### Manejo de errores

| Test | Qué verifica |
|---|---|
| `Expirar_CuandoRepoLanzaExcepcion_NoPropagarElError` | Excepción del repo → absorbida por el `try/catch` externo |
| `Expirar_CuandoSaveChangesLanzaExcepcion_NoPropagarElError` | Excepción de persistencia → absorbida |

### Ciclo de vida del scope

| Test | Qué verifica |
|---|---|
| `Expirar_SiempreCreaExactamenteUnScope` | Un scope por ejecución |
| `Expirar_DisponeElScopeAlFinalizar` | `using var scope` libera el scope |
| `Expirar_CuandoExcepcion_ElScopeSigueDisposed` | `Dispose()` se llama aunque haya excepción |

---

## Tests nuevos en `TurnoServiceCreateTests` (+3)

| Test | Qué verifica |
|---|---|
| `Create_OrigenWeb_ConSena_EstadoPendientePago` | Un turno Web con seña parte en `PendientePago` |
| `Create_OrigenWeb_ConSena_NoEnviaEmailDeConfirmacion` | El email de confirmación se omite mientras el pago no fue aprobado |
| `Create_FechaCreacion_SeEstableceAlCrear` | `FechaCreacion` se inicializa con `DateTime.Now` al persistir |

---

## Reglas de negocio cubiertas

| Regla | Tests que la cubren |
|---|---|
| Turno Web con seña → `PendientePago` hasta recibir webhook de pago aprobado | `Create_OrigenWeb_ConSena_*`, `GenerarLink_*`, `Procesar_PagoAprobado_Confirma*` |
| El slot queda bloqueado mientras el turno está en `PendientePago` | (cubierto por lógica de `GetActivosSucursalEnRangoAsync` — no excluye `PendientePago`) |
| Checkout abandonado (sin webhook de MP) → expiración a `PagoRechazado` en 30 min | `Expirar_ConTurnosVencidos_*`, `Expirar_FecheLimite_*` |
| `PagoRechazado` libera el slot | (cubierto por tests de `GetActivosSucursalEnRangoAsync` que excluye `PagoRechazado`) |
| Webhook duplicado (`approved`) → idempotencia | `Procesar_PagoAprobado_TurnoYaConfirmado_Idempotencia` |
| Pago rechazado/cancelado → `PagoRechazado`, no `CanceladoCliente` | `Procesar_PagoRechazadoOCancelado_*` |
| Fallo de email no revierte la confirmación | `Procesar_PagoAprobado_EmailFalla_*` |
| Sin Caja → confirma igual (no queda atascado) | `Procesar_PagoAprobado_SinCaja_*` |
| El scope de DI siempre se libera | `*_ElScopeSigueDisposed`, `*_DisponeElScope*` |
