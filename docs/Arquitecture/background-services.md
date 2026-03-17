# Background Services

Servicios de fondo registrados en `Program.cs` como `IHostedService`. Ambos heredan de `BackgroundService` y siguen el mismo patrón: crean un scope por ejecución via `IServiceScopeFactory` para resolver dependencias scoped (repositorios, servicios de email) desde un contexto singleton.

```csharp
builder.Services.AddHostedService<TurnoAutoNoShowService>();
builder.Services.AddHostedService<RecordatoriosEmailBackgroundService>();
```

---

## TurnoAutoNoShowService

**Archivo:** `src/Api/Services/TurnoAutoNoShowService.cs`

### Propósito

Marca automáticamente como `NoShow` los turnos que terminaron el día anterior y nunca fueron marcados como `Completado`.

### Frecuencia

Se ejecuta **una vez por día, a las 3:00 AM** (hora local del servidor).

El cálculo de espera se hace en cada ciclo:

```csharp
var proximas3AM = ahora.Date.AddDays(ahora.Hour < 3 ? 0 : 1).AddHours(3);
var espera      = proximas3AM - ahora;
await Task.Delay(espera, stoppingToken);
```

Si el servicio arranca antes de las 3 AM, espera hasta las 3 AM del mismo día. Si arranca después, espera hasta las 3 AM del día siguiente.

### Lógica

```
FechaLimite = DateTime.UtcNow.Date   (medianoche UTC de hoy)

Turnos afectados:
  Estado ∈ { Confirmado, Pendiente }
  AND FechaHoraFin < FechaLimite
```

Todos los turnos encontrados se actualizan en un único `UpdateRange` + `SaveChangesAsync`.

### Método de repositorio

`ITurnoRepository.GetConfirmadosPendientesAntesDeAsync(DateTime fechaLimite)`

Devuelve los turnos en estado `Confirmado` o `Pendiente` con `FechaHoraFin` anterior a la fecha límite.

### Comportamiento ante errores

Los errores se capturan con `try/catch` y se loguean con `LogError`. El servicio **no se detiene** — el bucle continúa y reintenta en la próxima ejecución programada.

---

## RecordatoriosEmailBackgroundService

**Archivo:** `src/Api/Services/RecordatoriosEmailBackgroundService.cs`

### Propósito

Envía emails de recordatorio a los clientes con turnos próximos. Soporta dos ventanas de tiempo: 24 horas antes y 3 horas antes del turno.

### Frecuencia

Se ejecuta **cada 15 minutos**, comenzando al arrancar la API (primera ejecución inmediata).

```csharp
private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(15);
```

### Flags en la entidad `Turno`

La entidad `Turno` almacena dos booleans para evitar envíos duplicados:

| Campo | Tipo | Default | Descripción |
|---|---|---|---|
| `Recordatorio24hEnviado` | `bool` | `false` | Se marca `true` al enviar el recordatorio de 24 h |
| `Recordatorio3hEnviado` | `bool` | `false` | Se marca `true` al enviar el recordatorio de 3 h |

### Ventanas de tiempo

| Ventana | Condición | Flag que controla |
|---|---|---|
| **3 horas** | `FechaHoraInicio ∈ (ahora, ahora + 3 h]` y `Recordatorio3hEnviado == false` | `Recordatorio3hEnviado` |
| **24 horas** | `FechaHoraInicio ∈ (ahora, ahora + 24 h]` y `Recordatorio24hEnviado == false` | `Recordatorio24hEnviado` |

### Caso especial: turno en ambas ventanas simultáneamente

Un turno creado con menos de 3 horas de antelación caería en ambas ventanas al mismo tiempo. En ese caso:

- Se envía **únicamente el recordatorio de 3 h**
- Se marcan **ambos flags** (`Recordatorio3hEnviado = true` y `Recordatorio24hEnviado = true`)
- Evita que el cliente reciba dos emails en el mismo ciclo o en el siguiente

### Flujo por ciclo

```
1. Calcular ahora, limite3h = ahora + 3h, limite24h = ahora + 24h
2. GetParaRecordatoriosAsync(ahora, limite24h, limite3h)
3. Por cada turno:
   a. Si no tiene email de cliente → saltar
   b. Si Recordatorio3hEnviado == false AND FechaHoraInicio ≤ limite3h:
      → Enviar email "3 h antes"
      → Recordatorio3hEnviado = true
      → Recordatorio24hEnviado = true   ← anti-spam
      → continue (no evaluar ventana 24 h para este turno)
   c. Si Recordatorio24hEnviado == false AND FechaHoraInicio ≤ limite24h:
      → Enviar email "24 h antes"
      → Recordatorio24hEnviado = true
4. UpdateRange(modificados) + SaveChangesAsync()
```

### Método de repositorio

`ITurnoRepository.GetParaRecordatoriosAsync(DateTime ahora, DateTime limite24h, DateTime limite3h)`

Devuelve turnos en estado `Confirmado` o `Pendiente`, con inicio futuro, donde al menos uno de los dos flags de recordatorio está en `false` y la fecha de inicio cae dentro de alguna ventana. Incluye las navegaciones `Cliente`, `Servicio`, `Profesional` y `Sucursal` para construir el email sin consultas adicionales.

### Configuración

La URL base del frontend se lee desde `appsettings.json`:

```json
"App": {
  "FrontendUrl": "https://miapp.com"
}
```

Se usa para construir el link de cancelación en el email:

```
{FrontendUrl}/turno/{TokenGestion}
```

### Comportamiento ante errores

- Error en un email individual: se loguea con `LogError` y se continúa con el siguiente turno (el flag **no** se marca, por lo que se reintentará en el próximo ciclo).
- Error global del ciclo: se loguea con `LogError`. El servicio **no se detiene** — reintenta 15 minutos después.

---

## Comparación entre ambos servicios

| | `TurnoAutoNoShowService` | `RecordatoriosEmailBackgroundService` |
|---|---|---|
| **Frecuencia** | Diaria a las 3:00 AM | Cada 15 minutos |
| **Primera ejecución** | A las 3:00 AM siguientes | Inmediatamente al arrancar |
| **Dependencias** | `ITurnoRepository` | `ITurnoRepository` + `IEmailService` |
| **Persistencia** | Actualiza `Estado` del turno | Actualiza flags `Recordatorio*hEnviado` |
| **Fallo de email** | N/A | No marca el flag → reintento automático |
| **Fallo de DB** | Se loguea, reintenta al día siguiente | Se loguea, reintenta en 15 min |
