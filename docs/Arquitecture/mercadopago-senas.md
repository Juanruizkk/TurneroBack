# Integración MercadoPago — Cobro de Señas Online

Documentación de referencia para el módulo de pagos online con Mercado Pago, aplicado al cobro de señas en el booking público.

---

## Índice

1. [Resumen del módulo](#1-resumen-del-módulo)
2. [Decisión de diseño: ¿quién paga la seña?](#2-decisión-de-diseño-quién-paga-la-seña)
3. [Máquina de estados del turno](#3-máquina-de-estados-del-turno)
4. [Cambios en el modelo de dominio](#4-cambios-en-el-modelo-de-dominio)
5. [Arquitectura de componentes](#5-arquitectura-de-componentes)
6. [Flujo técnico end-to-end](#6-flujo-técnico-end-to-end)
7. [Componente: `MercadoPagoGateway`](#7-componente-mercadopagogateway)
8. [Componente: `PagoTurnoService`](#8-componente-pagoturnoservice)
9. [Endpoints](#9-endpoints)
10. [DTOs](#10-dtos)
11. [Configuración](#11-configuración)
12. [Registro en Program.cs](#12-registro-en-programcs)
13. [Idempotencia y casos borde](#13-idempotencia-y-casos-borde)
14. [Impacto en módulos existentes](#14-impacto-en-módulos-existentes)
15. [Migraciones de base de datos](#15-migraciones-de-base-de-datos)
16. [Guía de prueba local (ngrok + sandbox)](#16-guía-de-prueba-local-ngrok--sandbox)
17. [Archivos involucrados](#17-archivos-involucrados)

---

## 1. Resumen del módulo

Este módulo permite cobrar señas online a través de **Mercado Pago Checkout Pro** cuando un cliente reserva un turno a través del booking público. El flujo es:

1. El cliente completa la reserva → el turno se crea en estado `PendientePago`.
2. El frontend llama a `POST /api/pagos/turnos/{id}/iniciar-pago` → recibe un `preferenceId`.
3. El frontend redirige al cliente a MercadoPago con ese `preferenceId`.
4. Cuando el cliente paga, MP llama al webhook `POST /api/webhooks/mercadopago`.
5. El webhook confirma el turno y registra el movimiento en Caja automáticamente.

```
Cliente (browser)                  Backend                        MercadoPago
      │                               │                               │
      │─── POST /api/turnos ──────────►│                               │
      │◄── 201 { estado: PendientePago }│                               │
      │                               │                               │
      │─── POST /api/pagos/turnos/{id}/iniciar-pago ──────────────────►│
      │                               │── CrearPreferencia ──────────►│
      │                               │◄── preferenceId ──────────────│
      │◄── 200 { preferenceId } ──────│                               │
      │                               │                               │
      │─── redirect a MP Checkout ───────────────────────────────────►│
      │◄── pantalla de pago ─────────────────────────────────────────│
      │─── cliente paga ─────────────────────────────────────────────►│
      │                               │◄─ webhook payment.approved ───│
      │                               │── confirmar turno + caja ─────│
      │◄── redirect /reserva/pago/exitoso ───────────────────────────│
```

> **Alcance:** este flujo aplica **únicamente al origen `Web`** (booking público). El staff (Admin / Profesional / Caja) crea turnos con seña directamente, y la seña se registra manualmente en Caja cuando el cliente la abona en persona.

---

## 2. Decisión de diseño: ¿quién paga la seña?

| Origen del turno | `requiereSena` | Estado inicial | Flujo de cobro |
|---|---|---|---|
| `Web` (booking público) | `true` | `PendientePago` | MercadoPago Checkout → webhook → `Confirmado` |
| `Web` (booking público) | `false` | Según `modoConfirmacionTurnos` | Sin cambios |
| `Administrativo` / `Profesional` / `Caja` | `true` | `Pendiente` | Seña registrada manualmente en Caja |
| `Administrativo` / `Profesional` / `Caja` | `false` | Según `modoConfirmacionTurnos` | Sin cambios |

### Regla sobre `modoConfirmacionTurnos`

El modo de confirmación (`Automático` / `Manual`) **solo aplica a turnos sin seña**. Los turnos online con seña siempre usan el flujo de pago, independientemente de la configuración del modo de confirmación.

```csharp
// TurnoService.CreateAsync — lógica de estado inicial
if (dto.Origen == OrigenTurno.Web && servicio.RequiereSena)
    estadoInicial = EstadoTurno.PendientePago;
else if (dto.Origen == OrigenTurno.Web && modo == ModoConfirmacionTurnos.Manual)
    estadoInicial = EstadoTurno.Pendiente;
else
    estadoInicial = EstadoTurno.Confirmado;
```

---

## 3. Máquina de estados del turno

### Estados completos (post-integración)

```
Confirmado ──────────────────────────────────────┐
    ▲                                             │
    │ webhook payment.approved                    │
    │                                             ▼
PendientePago ──── webhook rejected/cancelled ──► PagoRechazado
    │
    ├──── staff cancela manualmente ─────────────► CanceladoStaff
    └──── cliente cancela vía token ─────────────► CanceladoCliente
```

### Tabla de estados extendida

| Valor | Nombre            | Descripción                                                           |
|-------|-------------------|-----------------------------------------------------------------------|
| `0`   | `Pendiente`       | Turno creado, pendiente de confirmación manual por el staff           |
| `1`   | `Confirmado`      | Turno confirmado (por staff, modo automático, o pago MP aprobado)     |
| `2`   | `CanceladoCliente`| Cancelado por el cliente (token público o vencimiento de pago)        |
| `3`   | `CanceladoStaff`  | Cancelado por el staff desde el panel de administración               |
| `4`   | `Completado`      | Servicio realizado exitosamente                                        |
| `5`   | `NoShow`          | El cliente no se presentó                                             |
| `6`   | `Reprogramado`    | Turno movido a otro horario                                           |
| `7`   | `PendientePago`   | **Nuevo.** Turno `Web` con seña, esperando pago en MercadoPago        |
| `8`   | `PagoRechazado`   | **Nuevo.** El pago en MP fue rechazado o cancelado. Distinto de `CanceladoCliente` (cancelación voluntaria). |

### `PendientePago` en el contexto de conflictos de horario

Los turnos en estado `PendientePago` **bloquean el horario** hasta que se confirmen o expiren. Esto evita que dos clientes reserven el mismo slot simultáneamente mientras uno está en el proceso de pago. `ExisteConflictoHorarioAsync` trata `PendientePago` como un estado activo (no lo excluye).

Una vez que el turno pasa a `PagoRechazado` (por webhook de rechazo o por expiración automática), el slot queda libre porque ese estado sí está excluido de las consultas de conflicto.

---

## 4. Cambios en el modelo de dominio

### 4.1 `Turno` — nuevo campo `FechaCreacion`

```csharp
// Domain/Entities/Turno.cs
/// <summary>Fecha y hora en que fue creado el turno. Se usa para calcular expiración de PendientePago.</summary>
public DateTime FechaCreacion { get; set; }
```

| Propiedad | Tipo | Descripción |
|---|---|---|
| `FechaCreacion` | `DateTime` | Fecha y hora de creación (`Kind=Unspecified`, `timestamp without time zone`). Seteado en `TurnoService.CreateAsync` con `DateTime.Now`. |

**Por qué se necesita:** el mecanismo de expiración automática (`TurnoPagoExpirationService`) necesita saber cuánto tiempo lleva un turno en `PendientePago` para determinar si fue abandonado.

Configuración en `TurnoConfiguration.cs`:

```csharp
builder.Property(t => t.FechaCreacion)
    .IsRequired()
    .HasColumnType("timestamp without time zone");
```

Migración: `AddFechaCreacionToTurnos`

> **Registros existentes:** EF usará el default de `DateTime` (`0001-01-01`), lo cual es aceptable ya que los turnos históricos no están en `PendientePago`.

---

### 4.2 `Turno` — nuevo campo `MpPreferenceId`

```csharp
// Domain/Entities/Turno.cs
public string? MpPreferenceId { get; set; }
```

| Propiedad       | Tipo      | Descripción                                                                  |
|-----------------|-----------|------------------------------------------------------------------------------|
| `MpPreferenceId`| `string?` | ID de la preferencia creada en MP. Solo presente en turnos `PendientePago`. Se persiste para reutilización (idempotencia). |

Configuración en `TurnoConfiguration.cs`:

```csharp
builder.Property(t => t.MpPreferenceId)
    .HasMaxLength(200)
    .IsRequired(false);
```

### 4.3 `MovimientoCaja` — `UsuarioId` pasa a ser nullable

```csharp
// Domain/Entities/MovimientoCaja.cs  (ANTES)
public Guid  UsuarioId { get; set; }
public Usuario Usuario { get; set; } = null!;

// (DESPUÉS)
public Guid?  UsuarioId { get; set; }
public Usuario? Usuario  { get; set; }
```

**Motivo:** el webhook de MercadoPago confirma turnos y registra movimientos en Caja de forma automática, sin un usuario humano involucrado. Antes de este cambio, `UsuarioId` tenía una restricción de FK no-nullable que impedía insertar movimientos sin usuario. Ahora `null` significa "movimiento registrado por el sistema".

Configuración en `MovimientoCajaConfiguration.cs`:

```csharp
builder.HasOne(m => m.Usuario)
    .WithMany(u => u.MovimientosRegistrados)
    .HasForeignKey(m => m.UsuarioId)
    .IsRequired(false)           // ← nuevo
    .OnDelete(DeleteBehavior.Restrict);
```

> **Impacto en `MovimientoCajaResponseDto`:** el campo `UsuarioId` también cambió a `Guid?`. Los movimientos registrados vía webhook tendrán `usuarioId: null` en la respuesta de la API de Caja.

---

## 5. Arquitectura de componentes

```
src/
├── Domain/
│   ├── Entities/
│   │   ├── Turno.cs                      ← +MpPreferenceId, +FechaCreacion
│   │   └── MovimientoCaja.cs             ← UsuarioId nullable
│   └── Enums/
│       └── EstadoTurno.cs                ← +PendientePago = 7
│
├── Application/
│   ├── Interfaces/
│   │   ├── IMercadoPagoGateway.cs        ← nuevo: contrato del gateway externo
│   │   └── IPagoTurnoService.cs          ← nuevo: contrato del servicio de pagos
│   ├── DTOs/
│   │   └── Pagos/
│   │       ├── IniciarPagoResponseDto.cs ← nuevo: respuesta del endpoint iniciar-pago
│   │       └── PaymentDetailsDto.cs      ← nuevo: DTO interno de respuesta de MP
│   └── Services/
│       ├── TurnoService.cs               ← modificado: lógica PendientePago
│       └── PagoTurnoService.cs           ← nuevo: orquesta el flujo de pagos
│
├── Infrastructure/
│   ├── Data/Configurations/
│   │   ├── TurnoConfiguration.cs         ← +MpPreferenceId mapping
│   │   └── MovimientoCajaConfiguration.cs← UsuarioId IsRequired(false)
│   └── Services/MercadoPago/
│       ├── MercadoPagoOptions.cs         ← nuevo: opciones de configuración
│       └── MercadoPagoGateway.cs         ← nuevo: implementación del gateway (SDK MP)
│
└── Api/
    ├── Controllers/
    │   ├── PagosController.cs            ← nuevo: POST /api/pagos/turnos/{id}/iniciar-pago
    │   └── WebhooksController.cs         ← nuevo: POST /api/webhooks/mercadopago
    ├── Services/
    │   └── TurnoPagoExpirationService.cs ← nuevo: expira turnos PendientePago abandonados
    └── Program.cs                        ← +registros MP + TurnoPagoExpirationService
```

### Separación de responsabilidades

| Componente | Responsabilidad |
|---|---|
| `MercadoPagoGateway` | Única clase que conoce el SDK de MP. Llama a la API de MP, mapea respuestas a DTOs propios. No tiene lógica de negocio. |
| `PagoTurnoService` | Orquesta el flujo: verifica estado del turno, calcula montos, llama al gateway, registra en Caja. No conoce el SDK de MP directamente. |
| `PagosController` | HTTP adapter: extrae parámetros, maneja excepciones → HTTP codes. |
| `WebhooksController` | Recibe notificaciones de MP, responde `200 OK` inmediatamente, delega procesamiento en background. |

---

## 6. Flujo técnico end-to-end

### 6.1 Creación del turno (booking público)

```
POST /api/turnos
Body: { origen: "Web", servicioId: "...", ... }
```

1. `TurnoService.CreateAsync` detecta `Origen == Web && servicio.RequiereSena == true`.
2. Calcula `MontoSena` según `TipoSena` (fija o porcentaje).
3. Asigna `Estado = PendientePago`.
4. Persiste el turno y retorna el `TurnoResponseDto`.
5. El frontend detecta `estado == "PendientePago"` y llama a iniciar-pago.

### 6.2 Iniciar pago

```
POST /api/pagos/turnos/{turnoId}/iniciar-pago
Sin autenticación — el cliente lo llama desde el booking público.
```

1. `PagosController` → `PagoTurnoService.GenerarLinkDePagoAsync(turnoId)`.
2. Verifica que `turno.Estado == PendientePago` (lanza `InvalidOperationException` si no).
3. Si `turno.MpPreferenceId` ya existe, lo retorna directamente (idempotencia).
4. Calcula el monto: `turno.MontoSena > 0 ? MontoSena : PrecioServicio`.
5. Llama a `MercadoPagoGateway.CrearPreferenciaAsync(turnoId, titulo, monto, frontendUrl)`.
6. Persiste `turno.MpPreferenceId = preferenceId`.
7. Retorna `{ preferenceId }`.

El frontend usa el `preferenceId` para redirigir al cliente a:
```
https://www.mercadopago.com.ar/checkout/v1/redirect?preference-id={preferenceId}
```
O bien renderiza el **MP Brick** usando la `PublicKey` configurada.

### 6.3 Webhook de pago

```
POST /api/webhooks/mercadopago
Sin autenticación — llamado por los servidores de MP.
```

MP puede enviar la notificación en dos formatos:

**Formato legacy (IPN):**
```
POST /api/webhooks/mercadopago?type=payment&data.id=123456789
```

**Formato nuevo (Webhooks v2):**
```json
POST /api/webhooks/mercadopago
{
  "type": "payment",
  "data": { "id": "123456789" }
}
```

El controller maneja ambos formatos y siempre responde `200 OK` para que MP no reintente.

#### Procesamiento en `PagoTurnoService.ProcesarNotificacionPagoAsync`

```
1.  Consulta detalles del pago a MP (GET /v1/payments/{paymentId})
2.  Parsea ExternalReference como Guid → turnoId
3.  Carga turno con relaciones
4.  Si status == "rejected" o "cancelled" y turno.Estado == PendientePago
      → turno.Estado = CanceladoCliente, persiste y return
5.  Si status != "approved" → log informativo + return (ej: "pending")
6.  Idempotencia: si turno.Estado == Confirmado → return
7.  Actualiza turno: Estado = Confirmado, TotalCobrado += pago.Amount
8.  Obtiene Caja de la sucursal
9.  Si no hay Caja → confirma turno de todas formas (sin movimiento de caja), log error
10. Crea MovimientoCaja (UsuarioId = null, MedioPago = MercadoPago)
11. Crea PagoTurno (Concepto = Sena)
12. RegistrarMovimientoTransaccionalAsync → transacción atómica
13. Si el cliente tiene email → envía email de confirmación de turno
```

#### Procesamiento sincrónico

El webhook se procesa de forma **sincrónica** (sin `Task.Run`) antes de retornar `200 OK`. Esto garantiza que el `DbContext` esté vivo durante toda la operación:

```csharp
// WebhooksController.cs
try
{
    await _pagoService.ProcesarNotificacionPagoAsync(paymentId);
}
catch (Exception ex)
{
    _logger.LogError(ex, "Error procesando PaymentId={Id}", paymentId);
}
return Ok();
```

> MP puede reintentar el webhook si no recibe `200 OK`. Al procesar sincrónicamente, MP espera el resultado real antes de decidir si reintenta. Los errores se loguean y aun así se responde `200 OK` para evitar reintentos infinitos.

---

## 7. Componente: `MercadoPagoGateway`

**Archivo:** `src/Infrastructure/Services/MercadoPago/MercadoPagoGateway.cs`
**Interfaz:** `src/Application/Interfaces/IMercadoPagoGateway.cs`
**Paquete NuGet:** `mercadopago-sdk 2.11.0`

### `CrearPreferenciaAsync`

Crea una **Preferencia de Pago** en la API de MP y devuelve el `preferenceId`.

```csharp
Task<string> CrearPreferenciaAsync(
    string  turnoId,          // se guarda como ExternalReference
    string  tituloServicio,   // descripción del ítem
    decimal monto,            // monto de la seña
    string  frontendUrl)      // base URL del frontend (para BackUrls)
```

**Parámetros clave de la preferencia:**

| Campo             | Valor                                                        |
|-------------------|--------------------------------------------------------------|
| `ExternalReference` | `turnoId.ToString()` — identifica el turno en el webhook   |
| `CurrencyId`       | `"ARS"`                                                     |
| `AutoReturn`       | `"approved"` — solo si `frontendUrl` no contiene `"localhost"` |
| `NotificationUrl`  | URL del webhook configurada en `appsettings.json` (null si vacía) |
| `BackUrls.Success` | `{frontendUrl}/reserva/pago/exitoso` — solo en producción   |
| `BackUrls.Failure` | `{frontendUrl}/reserva/pago/fallido` — solo en producción   |
| `BackUrls.Pending` | `{frontendUrl}/reserva/pago/pendiente` — solo en producción |

> **Comportamiento en localhost:** `BackUrls` y `AutoReturn` se omiten cuando `frontendUrl` contiene `"localhost"` porque MP rechaza URLs privadas con error `auto_return invalid`. En desarrollo, el cliente debe cerrar la ventana de MP manualmente tras el pago; el webhook igual procesa el resultado.

### `ObtenerDetallesPagoAsync`

Consulta el estado real de un pago en la API de MP dado su `paymentId`.

```csharp
Task<PaymentDetailsDto> ObtenerDetallesPagoAsync(long paymentId)
```

> **Por qué consultar MP en lugar de confiar en el body del webhook:** MP recomienda siempre consultar el estado real del pago vía API para evitar ataques donde alguien envía un webhook falso con `"status": "approved"`. La fuente de verdad es siempre la API de MP.

### Configuración del SDK

```csharp
// Se configura en el constructor (token global del SDK)
MercadoPagoConfig.AccessToken = _options.AccessToken;

// Y también se pasa por request para mayor seguridad
var requestOptions = new RequestOptions { AccessToken = _options.AccessToken };
```

---

## 8. Componente: `PagoTurnoService`

**Archivo:** `src/Application/Services/PagoTurnoService.cs`
**Interfaz:** `src/Application/Interfaces/IPagoTurnoService.cs`

### Dependencias

| Dependencia | Uso |
|---|---|
| `ITurnoRepository` | Cargar y actualizar el turno |
| `ICajaRepository` | Obtener la Caja y registrar el movimiento transaccional |
| `ISucursalRepository` | Obtener el nombre de la sucursal para el email de confirmación |
| `IMercadoPagoGateway` | Crear preferencias y consultar pagos en MP |
| `IEmailService` | Enviar email de confirmación al cliente tras el pago aprobado |
| `ILogger<PagoTurnoService>` | Logging de operaciones y errores |
| `string frontendUrl` | Base URL del frontend para construir BackUrls y `cancelUrl` del email |

### Método `GenerarLinkDePagoAsync`

```csharp
Task<string> GenerarLinkDePagoAsync(Guid turnoId)
// Retorna: preferenceId de MP
```

**Pre-condiciones:**
- El turno debe existir (`404` si no).
- El turno debe estar en estado `PendientePago` (`400` si no).

**Lógica de monto:**
```
monto = (turno.MontoSena > 0) ? turno.MontoSena : turno.PrecioServicio
```
Si el servicio tiene seña configurada, se cobra la seña. Si por alguna razón `MontoSena` es nulo o cero (caso defensivo), se cobra el precio completo del servicio.

### Método `ProcesarNotificacionPagoAsync`

```csharp
Task ProcesarNotificacionPagoAsync(long paymentId)
```

**Construcción del `MovimientoCaja` para el webhook:**

```csharp
new MovimientoCaja
{
    Id             = Guid.NewGuid(),
    CajaId         = caja.Id,
    TurnoId        = turno.Id,
    UsuarioId      = null,               // sistema — sin usuario humano
    TipoMovimiento = TipoMovimientoCaja.CobroTurno,
    MedioPago      = MedioPago.MercadoPago,
    Monto          = pago.Amount,
    Observaciones  = $"Seña cobrada automáticamente vía Mercado Pago (PaymentId: {paymentId})",
    Fecha          = DateTime.UtcNow,
}
```

**Construcción del `PagoTurno`:**

```csharp
new PagoTurno
{
    Id               = Guid.NewGuid(),
    TurnoId          = turno.Id,
    MovimientoCajaId = movimiento.Id,
    Concepto         = ConceptoPagoTurno.Sena,
    Monto            = pago.Amount,
    Fecha            = movimiento.Fecha,
}
```

Todo se persiste en una sola transacción via `ICajaRepository.RegistrarMovimientoTransaccionalAsync`.

Tras la transacción, si el cliente tiene email registrado, se envía el email de confirmación:

```csharp
await _emailService.EnviarConfirmacionTurnoAsync(
    clienteEmail,
    clienteNombre,
    sucursalNombre,      // obtenido via ISucursalRepository
    servicioNombre,
    profesionalNombre,
    turno.FechaHoraInicio,
    turno.FechaHoraFin,
    cancelUrl            // {frontendUrl}/turno/{turno.TokenGestion}
);
```

El envío del email es no-crítico: si falla, se loguea con `LogWarning` pero el turno queda `Confirmado` de todas formas.

---

## 9. Endpoints

### `POST /api/pagos/turnos/{turnoId:guid}/iniciar-pago`

Genera un link de pago de MercadoPago para el turno indicado.

| Aspecto | Detalle |
|---|---|
| Autenticación | **Sin autenticación** — lo llama el cliente desde el booking público |
| Idempotencia | Si el turno ya tiene `MpPreferenceId`, devuelve el mismo sin crear uno nuevo |
| Controller | `Api/Controllers/PagosController.cs` |

**Request:** sin body — el `turnoId` va en la URL.

**Response exitosa (`200 OK`):**
```json
{
  "preferenceId": "1234567890-abcd1234-ef56-..."
}
```

**Respuestas de error:**

| Código | Condición |
|--------|-----------|
| `400 Bad Request` | El turno no está en estado `PendientePago` |
| `404 Not Found` | El `turnoId` no existe |

---

### `POST /api/webhooks/mercadopago`

Endpoint que recibe notificaciones de cambio de estado de pagos desde MercadoPago.

| Aspecto | Detalle |
|---|---|
| Autenticación | **Sin autenticación** — lo llama MP directamente |
| Respuesta | Siempre `200 OK` (para evitar reintentos de MP) |
| Procesamiento | Fire-and-forget en `Task.Run` background |
| Controller | `Api/Controllers/WebhooksController.cs` |

**Formatos de entrada soportados:**

```
# Formato IPN (query string)
POST /api/webhooks/mercadopago?type=payment&data.id=123456789

# Formato Webhooks v2 (body JSON)
POST /api/webhooks/mercadopago
Content-Type: application/json
{
  "type": "payment",
  "data": { "id": "123456789" }
}
```

**Eventos procesados:**

| `type` | `status` | Acción |
|--------|----------|--------|
| `payment` | `"approved"` | Confirma el turno + registra en Caja + envía email al cliente |
| `payment` | `"rejected"` / `"cancelled"` | Marca el turno como `PagoRechazado` (libera el slot) |
| `payment` | `"pending"` u otro | Log informativo, sin acción |
| Cualquier otro tipo | — | Responde `200 OK`, sin procesamiento |

---

## 10. DTOs

### `IniciarPagoResponseDto`

**Archivo:** `Application/DTOs/Pagos/IniciarPagoResponseDto.cs`

```json
{
  "preferenceId": "string"
}
```

| Campo | Tipo | Descripción |
|---|---|---|
| `preferenceId` | `string` | ID de la preferencia de MP. El frontend lo usa para inicializar el Brick o construir la URL de redirección. |

---

### `PaymentDetailsDto`

**Archivo:** `Application/DTOs/Pagos/PaymentDetailsDto.cs`

DTO interno que mapea la respuesta de `GET /v1/payments/{id}` de la API de MP. No se expone en la API REST del backend.

| Campo | Tipo | Descripción |
|---|---|---|
| `PaymentId` | `long` | ID del pago en MP |
| `Status` | `string` | `"approved"` / `"rejected"` / `"pending"` / `"cancelled"` |
| `StatusDetail` | `string` | Detalle del status (ej: `"accredited"`) |
| `ExternalReference` | `string` | El `turnoId` que se guardó al crear la preferencia |
| `Amount` | `decimal` | Monto del pago (`TransactionAmount`) |

---

### `MpWebhookPayload` / `MpWebhookData`

**Archivo:** `Api/Controllers/WebhooksController.cs` (definidos en el mismo archivo, final)

```csharp
public sealed class MpWebhookPayload
{
    public string?       Type { get; set; }
    public MpWebhookData? Data { get; set; }
}

public sealed class MpWebhookData
{
    public string? Id { get; set; }
}
```

Mapean el body del webhook v2 de MercadoPago.

---

## 11. Configuración

### `appsettings.json`

```json
{
  "MercadoPago": {
    "AccessToken": "TEST-xxxx",
    "PublicKey": "TEST-yyyy",
    "NotificationUrl": "https://tudominio.com/api/webhooks/mercadopago"
  },
  "App": {
    "FrontendUrl": "http://localhost:5173"
  }
}
```

| Campo | Descripción | Dónde obtenerlo |
|---|---|---|
| `AccessToken` | Token privado del vendedor. **Nunca exponer al frontend.** | MP Developers → Credenciales |
| `PublicKey` | Clave pública para inicializar el SDK del frontend (MP Brick). | MP Developers → Credenciales |
| `NotificationUrl` | URL pública que MP llama para notificaciones de pago. Debe ser HTTPS en producción. | Configurar al hacer deploy |

### `MercadoPagoOptions`

**Archivo:** `Infrastructure/Services/MercadoPago/MercadoPagoOptions.cs`

```csharp
public sealed class MercadoPagoOptions
{
    public const string SectionName = "MercadoPago";
    public string AccessToken    { get; set; } = string.Empty;
    public string PublicKey      { get; set; } = string.Empty;
    public string NotificationUrl{ get; set; } = string.Empty;
}
```

Enlazado en `Program.cs` con:
```csharp
builder.Services.Configure<MercadoPagoOptions>(
    builder.Configuration.GetSection(MercadoPagoOptions.SectionName));
```

---

## 12. Registro en Program.cs

```csharp
// ─── MercadoPago ─────────────────────────────────────────────────────────────
builder.Services.Configure<MercadoPagoOptions>(
    builder.Configuration.GetSection(MercadoPagoOptions.SectionName));

builder.Services.AddScoped<IMercadoPagoGateway, MercadoPagoGateway>();

builder.Services.AddScoped<IPagoTurnoService>(sp => new PagoTurnoService(
    sp.GetRequiredService<ITurnoRepository>(),
    sp.GetRequiredService<ICajaRepository>(),
    sp.GetRequiredService<ISucursalRepository>(),
    sp.GetRequiredService<IMercadoPagoGateway>(),
    sp.GetRequiredService<IEmailService>(),
    sp.GetRequiredService<ILogger<PagoTurnoService>>(),
    builder.Configuration["App:FrontendUrl"] ?? "http://localhost:5173"
));
```

`PagoTurnoService` usa inyección manual (fábrica `sp =>`) porque recibe `frontendUrl` como primitivo, siguiendo el mismo patrón que `TurnoService` y `UsuarioService`.

---

## 13. Idempotencia y casos borde

### Idempotencia en `GenerarLinkDePagoAsync`

Si el frontend llama múltiples veces a `POST /api/pagos/turnos/{id}/iniciar-pago` para el mismo turno (por ejemplo, si el usuario cierra y reabre la página), el servicio devuelve el mismo `preferenceId` sin crear una nueva preferencia en MP:

```csharp
if (!string.IsNullOrEmpty(turno.MpPreferenceId))
    return turno.MpPreferenceId;  // reutiliza sin llamar a MP
```

### Idempotencia en `ProcesarNotificacionPagoAsync`

Si MP envía el mismo webhook más de una vez (MP garantiza "at least once delivery"), el servicio verifica el estado antes de procesar:

```csharp
if (turno.Estado == EstadoTurno.Confirmado)
{
    _logger.LogInformation("Turno ya confirmado. Webhook ignorado.");
    return;
}
```

### Turno sin Caja

Si la sucursal del turno no tiene una `Caja` asociada (situación de datos corruptos), el servicio:

1. Confirma el turno de todas formas (`Estado = Confirmado`, `TotalCobrado += monto`).
2. Omite la creación del movimiento de caja.
3. Registra un `LogError` para investigación.

Esto evita que el turno quede atascado en `PendientePago` indefinidamente por un problema de datos ajeno al pago.

### `ExternalReference` inválido

Si el `ExternalReference` del pago no puede parsearse como `Guid` válido (por ejemplo, si la preferencia se creó para otro sistema en la misma cuenta de MP), el webhook lo ignora con un `LogWarning` y devuelve `200 OK`.

### Pagos rechazados (`rejected` / `cancelled`)

Cuando MP envía un webhook con `status == "rejected"` o `"cancelled"`, el turno pasa a `PagoRechazado` si estaba en `PendientePago`. Esto libera el slot de la agenda y permite distinguirlo de una cancelación voluntaria del cliente en los reportes. Si el turno ya estaba en otro estado (por ejemplo ya cancelado manualmente), no se modifica.

### Pagos en estado `pending`

MP puede enviar webhooks para pagos en estado `pending` (transferencia bancaria, etc.). El servicio los ignora sin modificar el turno. Solo se procesan `"approved"`, `"rejected"` y `"cancelled"`.

---

## 14. Impacto en módulos existentes

### `TurnoService.CreateAsync`

Dos modificaciones respecto al comportamiento original:

**1. Lógica de estado inicial** (el cálculo de `MontoSena` ya existía):


```csharp
// Antes: solo dos casos
var estadoInicial = (dto.Origen == OrigenTurno.Web && modo == Manual)
    ? EstadoTurno.Pendiente
    : EstadoTurno.Confirmado;

// Después: tres casos
EstadoTurno estadoInicial;
if (dto.Origen == OrigenTurno.Web && servicio.RequiereSena)
    estadoInicial = EstadoTurno.PendientePago;
else if (dto.Origen == OrigenTurno.Web && modo == ModoConfirmacionTurnos.Manual)
    estadoInicial = EstadoTurno.Pendiente;
else
    estadoInicial = EstadoTurno.Confirmado;
```

**2. Email de confirmación omitido para `PendientePago`:**

```csharp
// Solo se envía email si el turno NO está esperando pago
if (estadoInicial != EstadoTurno.PendientePago && !string.IsNullOrWhiteSpace(cliente.Email))
{
    await _emailService.EnviarConfirmacionTurnoAsync(...);
}
```

El email de confirmación para turnos `PendientePago` lo envía `PagoTurnoService` tras recibir el webhook de pago aprobado. Esto evita que el cliente reciba un email de "turno confirmado" antes de haber completado el pago.

### `CajaService` y `MovimientoCajaResponseDto`

`UsuarioId` en `MovimientoCajaResponseDto` pasó de `Guid` a `Guid?`. Los endpoints existentes de Caja seguirán funcionando, pero los clientes del frontend que consuman esta propiedad deben manejar el caso `null` (movimiento automático del sistema).

### `ExisteConflictoHorarioAsync` (implícito)

El estado `PendientePago` bloquea horarios automáticamente porque la consulta SQL en `TurnoRepository` excluye solo los estados `CanceladoCliente`, `CanceladoStaff` y `NoShow`. `PendientePago` queda incluido en los conflictos.

---

## 15. Migraciones de base de datos

Se generaron dos migraciones en esta implementación:

### `20260318152628_AddMercadoPagoFields`

Agrega las columnas de plantillas WhatsApp que estaban pendientes en el snapshot del modelo. **No relacionada directamente con MP**, pero era necesario el snapshot estar al día.

```sql
ALTER TABLE configuraciones_sucursal
    ADD COLUMN "MensajeWhatsApp24h" character varying(2000),
    ADD COLUMN "MensajeWhatsApp3h"  character varying(2000);
```

### `20260318152832_AddMercadoPagoFieldsV2`

La migración principal de esta integración:

```sql
-- Nueva columna en turnos para el ID de preferencia de MP
ALTER TABLE turnos ADD COLUMN "MpPreferenceId" character varying(200);

-- UsuarioId pasa a nullable en movimientos_caja
ALTER TABLE movimientos_caja ALTER COLUMN "UsuarioId" DROP NOT NULL;
```

**Cómo aplicar en un entorno nuevo:**

```bash
dotnet ef database update --project src/Infrastructure --startup-project src/Api
```

**Cómo revertir:**

```bash
dotnet ef database update 20260316150855_AgregarPlantillasWhatsApp \
    --project src/Infrastructure --startup-project src/Api
```

---

## 16. Guía de prueba local (ngrok + sandbox)

### Paso 1 — Credenciales de prueba

Crear una cuenta en [https://www.mercadopago.com.ar/developers](https://www.mercadopago.com.ar/developers) y obtener las credenciales de **test** (no producción).

Actualizar `appsettings.Development.json`:

```json
{
  "MercadoPago": {
    "AccessToken": "TEST-1234567890abcdef-...",
    "PublicKey": "TEST-abcdef1234...",
    "NotificationUrl": "https://<tu-subdominio>.ngrok-free.app/api/webhooks/mercadopago"
  }
}
```

### Paso 2 — Exponer el backend con ngrok

```bash
# Instalar ngrok si no está instalado
# https://ngrok.com/download

# Exponer el puerto del backend (5000 por defecto)
ngrok http 5000
```

Copiar la URL HTTPS generada (ej: `https://abc123.ngrok-free.app`) y actualizar `NotificationUrl` en `appsettings.Development.json`.

### Paso 3 — Reiniciar el backend

```bash
cd backend
dotnet watch run --project src/Api
```

### Paso 4 — Crear un turno de prueba

```bash
curl -X POST http://localhost:5000/api/turnos \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <jwt-token>" \
  -d '{
    "origen": 0,
    "sucursalId": "<guid>",
    "clienteId": "<guid>",
    "servicioId": "<guid-servicio-con-sena>",
    "profesionalId": "<guid>",
    "fechaHoraInicio": "2026-03-20T10:00:00"
  }'
```

Verificar que la respuesta tenga `"estado": "PendientePago"` y `"montoSena"` con valor.

### Paso 5 — Iniciar el pago

```bash
curl -X POST http://localhost:5000/api/pagos/turnos/<turnoId>/iniciar-pago
```

Respuesta esperada:
```json
{ "preferenceId": "1234567890-xxxx-yyyy-..." }
```

### Paso 6 — Pagar en el sandbox de MP

Abrir en el browser:
```
https://www.mercadopago.com.ar/checkout/v1/redirect?preference-id=<preferenceId>
```

Usar las tarjetas de prueba de MP:
- **Aprobado:** `4509953566233704` — CVV: `123` — Venc: `11/25` — Nombre: `APRO`
- **Rechazado:** `4509953566233704` — CVV: `123` — Venc: `11/25` — Nombre: `OTHE`

### Paso 7 — Verificar el resultado en la DB

```bash
docker exec turnero_db psql -U postgres -d turnero -c "
SELECT t.\"Id\", t.\"Estado\", t.\"TotalCobrado\", t.\"MpPreferenceId\"
FROM turnos t
WHERE t.\"Id\" = '<turnoId>';
"
```

Estado esperado: `1` (`Confirmado`), `TotalCobrado` = monto de la seña.

```bash
docker exec turnero_db psql -U postgres -d turnero -c "
SELECT m.\"Id\", m.\"MedioPago\", m.\"Monto\", m.\"UsuarioId\", m.\"Observaciones\"
FROM movimientos_caja m
WHERE m.\"TurnoId\" = '<turnoId>';
"
```

Movimiento esperado: `MedioPago = 1` (`MercadoPago`), `UsuarioId = null`, `Observaciones` con el `PaymentId`.

---

## 17. Expiración automática de turnos `PendientePago` abandonados

### Problema

Cuando el cliente hace click en "Pagar con MercadoPago", el turno se crea en `PendientePago` y el slot queda reservado. Si el cliente **abandona el checkout** (cierra la pestaña, no ingresa la tarjeta, etc.), MP **no envía ningún webhook**, por lo que el slot quedaría bloqueado indefinidamente.

### Solución: `TurnoPagoExpirationService`

Un `BackgroundService` que se ejecuta cada 15 minutos y marca como `PagoRechazado` los turnos en `PendientePago` con `FechaCreacion` anterior a 30 minutos:

```
Limite = DateTime.Now - 30 minutos
Turnos vencidos: Estado == PendientePago AND FechaCreacion < Limite
```

El timeout de 30 minutos coincide con la ventana de sesión activa del checkout de MP.

### Flujo del checkout abandonado

```
Cliente (browser)                  Backend                        MercadoPago
      │                               │                               │
      │─── POST /api/turnos ──────────►│ (Estado = PendientePago)     │
      │─── POST /api/pagos/.../iniciar─►│── CrearPreferencia ─────────►│
      │◄── { preferenceId } ──────────│                               │
      │─── redirect a MP Checkout ───────────────────────────────────►│
      │  [cliente cierra la pestaña]  │                               │
      │                               │  [MP no envía webhook]        │
      │                               │                               │
      │                    (30 min después)                            │
      │                               │                               │
      │           TurnoPagoExpirationService ejecuta                  │
      │                               │── Estado = PagoRechazado      │
      │                               │   (slot liberado)             │
```

### Campos requeridos

| Campo | Por qué se necesita |
|---|---|
| `Turno.FechaCreacion` | Saber cuánto tiempo lleva el turno en `PendientePago` |

### Método de repositorio

`ITurnoRepository.GetPendientePagoVencidosAsync(DateTime limite)` — devuelve los turnos en `PendientePago` con `FechaCreacion < limite`.

### Registro en Program.cs

```csharp
builder.Services.AddHostedService<TurnoPagoExpirationService>();
```

---

## 18. Archivos involucrados

### Modificados

| Capa | Archivo | Cambio |
|---|---|---|
| Domain | `Domain/Enums/EstadoTurno.cs` | `+PendientePago = 7`, `+PagoRechazado = 8` |
| Domain | `Domain/Entities/Turno.cs` | `+MpPreferenceId: string?` |
| Domain | `Domain/Entities/MovimientoCaja.cs` | `UsuarioId: Guid → Guid?`, nav `Usuario?` |
| Infrastructure | `Infrastructure/Data/Configurations/TurnoConfiguration.cs` | Mapping `MpPreferenceId` (varchar 200) |
| Infrastructure | `Infrastructure/Data/Configurations/MovimientoCajaConfiguration.cs` | FK `UsuarioId` → `IsRequired(false)` |
| Infrastructure | `Infrastructure/TurneroApp.Infrastructure.csproj` | `+mercadopago-sdk 2.11.0` |
| Application | `Application/DTOs/Caja/MovimientoCajaResponseDto.cs` | `UsuarioId: Guid → Guid?` |
| Application | `Application/Services/TurnoService.cs` | Lógica `PendientePago` en `CreateAsync`; `FechaCreacion = DateTime.Now`; email omitido para `PendientePago` |
| Application | `Application/Services/PagoTurnoService.cs` | `+ISucursalRepository`, `+IEmailService`; usa `PagoRechazado` para pagos rechazados; envía email al confirmar |
| Api | `Api/Controllers/WebhooksController.cs` | Procesamiento sincrónico (sin `Task.Run`) |
| Api | `Api/Program.cs` | Registros: Options, Gateway, PagoTurnoService (con `ISucursalRepository` e `IEmailService`) |
| Api | `Api/appsettings.json` | Sección `"MercadoPago"` con credenciales reales |
| Frontend | `types/index.ts` | `+PendientePago`, `+PagoRechazado` en `EstadoTurno` |
| Frontend | `components/ui/StatusBadge.tsx` | Badges para `PendientePago` y `PagoRechazado` |
| Frontend | `pages/admin/Agenda.tsx` | `PagoRechazado` en `ESTADOS_GHOST` (se oculta con cancelados) |
| Frontend | `pages/public/BookingFlow.tsx` | Botón MP funcional: crea turno → `iniciarPago` → redirect a MP |
| Frontend | `api/pagos.ts` | Cliente API para `POST /api/pagos/turnos/{id}/iniciar-pago` |

### Creados

| Capa | Archivo | Descripción |
|---|---|---|
| Infrastructure | `Infrastructure/Services/MercadoPago/MercadoPagoOptions.cs` | POCO de configuración |
| Infrastructure | `Infrastructure/Services/MercadoPago/MercadoPagoGateway.cs` | Implementación del gateway (SDK MP) |
| Application | `Application/Interfaces/IMercadoPagoGateway.cs` | Contrato del gateway |
| Application | `Application/Interfaces/IPagoTurnoService.cs` | Contrato del servicio de pagos |
| Application | `Application/DTOs/Pagos/PaymentDetailsDto.cs` | DTO interno: respuesta de pago de MP |
| Application | `Application/DTOs/Pagos/IniciarPagoResponseDto.cs` | DTO response de `iniciar-pago` |
| Application | `Application/Services/PagoTurnoService.cs` | Orquesta el flujo completo: genera preferencia, confirma turno, registra en Caja, envía email |
| Api | `Api/Controllers/PagosController.cs` | `POST /api/pagos/turnos/{id}/iniciar-pago` |
| Api | `Api/Controllers/WebhooksController.cs` | `POST /api/webhooks/mercadopago` |
| Frontend | `pages/public/PagoExitoso.tsx` | Página de resultado exitoso (`/reserva/pago/exitoso`) |
| Frontend | `pages/public/PagoFallido.tsx` | Página de resultado fallido (`/reserva/pago/fallido`) |

### Migraciones

| Archivo | Descripción |
|---|---|
| `Migrations/20260318152628_AddMercadoPagoFields.cs` | WhatsApp columns (snapshot sync) |
| `Migrations/20260318152832_AddMercadoPagoFieldsV2.cs` | `MpPreferenceId` en turnos + `UsuarioId` nullable |

---

| Migraciones | `Migrations/AddFechaCreacionToTurnos.cs` | `FechaCreacion NOT NULL` en turnos |

### Creados (expiración automática)

| Capa | Archivo | Descripción |
|---|---|---|
| Api | `Api/Services/TurnoPagoExpirationService.cs` | BackgroundService: cada 15 min expira turnos PendientePago con FechaCreacion > 30 min |

---

*Documentación actualizada el 2026-03-18.*
