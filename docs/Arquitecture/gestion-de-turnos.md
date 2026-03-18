# Gestión de Turnos

Documento de referencia para el módulo de turnos del sistema TurneroApp.

---

## Entidad `Turno`

Definida en `src/Domain/Entities/Turno.cs`. Es el agregado central del sistema.

| Propiedad         | Tipo            | Descripción                                                     |
|-------------------|-----------------|-----------------------------------------------------------------|
| `Id`              | `Guid`          | Identificador único, asignado manualmente                       |
| `SucursalId`      | `Guid`          | Sucursal a la que pertenece el turno                            |
| `ClienteId`       | `Guid`          | Cliente que reservó                                             |
| `ServicioId`      | `Guid`          | Servicio a realizar                                             |
| `ProfesionalId`   | `Guid`          | Usuario profesional asignado                                    |
| `FechaHoraInicio` | `DateTime`      | Inicio del turno (sin zona horaria, `Kind=Unspecified`)         |
| `FechaHoraFin`    | `DateTime`      | Fin calculado automáticamente (`Inicio + Servicio.DuracionAgendaMin`) |
| `Estado`          | `EstadoTurno`   | Estado actual del turno (ver sección Estados)                   |
| `Origen`          | `OrigenTurno`   | Canal por el que fue creado (ver sección Origen)                |
| `Observaciones`   | `string?`       | Notas libres del staff o del cliente                            |
| `TokenGestion`    | `string`        | Token único para que el cliente gestione su turno sin login     |
| `PrecioServicio`  | `decimal`       | Precio del servicio al momento de crear el turno (snapshot)     |
| `MontoSena`       | `decimal?`      | Monto de seña requerido (calculado si `Servicio.RequiereSena`)  |
| `TotalCobrado`    | `decimal`       | Suma de todos los pagos registrados                             |
| `MpPreferenceId`  | `string?`       | ID de preferencia en MercadoPago. Solo presente cuando `Estado = PendientePago`. |
| `FechaCreacion`   | `DateTime`      | Fecha y hora de creación del turno (`Kind=Unspecified`). Se usa para calcular expiración de `PendientePago`. |

### Relaciones

- `Sucursal` → pertenece a una sucursal
- `Cliente` → cliente que reservó
- `Servicio` → servicio contratado
- `Profesional` → usuario asignado (`Usuario` con rol Profesional o Administrativo)
- `Comentarios` → lista de `TurnoComentario` (cascade delete)
- `Adjuntos` → lista de `TurnoAdjunto` (cascade delete)
- `Pagos` → lista de `PagoTurno` (restrict delete)

---

## Estados (`EstadoTurno`)

Definidos en `src/Domain/Enums/EstadoTurno.cs`. Persistidos como `int` en la DB.

| Valor | Nombre            | Descripción                                                         |
|-------|-------------------|---------------------------------------------------------------------|
| `0`   | `Pendiente`       | Estado inicial al crear un turno. Aún no confirmado por el staff                    |
| `1`   | `Confirmado`      | El staff confirmó la asistencia, o el pago online fue aprobado por MP               |
| `2`   | `CanceladoCliente`| El cliente canceló (via token público, o vencimiento de pago sin abonar)            |
| `3`   | `CanceladoStaff`  | El staff canceló desde el panel de administración                                   |
| `4`   | `Completado`      | El servicio fue realizado exitosamente                                               |
| `5`   | `NoShow`          | El cliente no se presentó al turno                                                  |
| `6`   | `Reprogramado`    | El turno fue movido a otro horario                                                  |
| `7`   | `PendientePago`   | Turno `Web` con seña requerida. Esperando pago online vía MercadoPago               |
| `8`   | `PagoRechazado`   | El pago en MercadoPago fue rechazado o cancelado. Distinto de una cancelación voluntaria del cliente (`CanceladoCliente`). No bloquea horario. |

### Transiciones habituales

```
PendientePago ──► webhook approved ──► Confirmado ──► Completado
     │                                      │
     ├──► webhook rejected/cancelled ──► PagoRechazado
     ├──► CanceladoCliente                  └──► CanceladoStaff
     └──► CanceladoStaff                    └──► NoShow
                                            └──► Reprogramado

Pendiente ──► Confirmado ──► Completado
    │               │
    │               └──► CanceladoStaff
    │               └──► NoShow
    │               └──► Reprogramado
    └──► CanceladoCliente  (desde la vista pública)
    └──► CanceladoStaff    (desde el panel admin)
```

### Estados "activos" (para conflictos de horario)

`ExisteConflictoHorarioAsync` excluye los estados cancelados y NoShow al verificar superposición:

```
Activos (bloquean horario): Pendiente, PendientePago, Confirmado, Completado, Reprogramado
Inactivos (no bloquean):    CanceladoCliente, CanceladoStaff, NoShow, PagoRechazado
```

> `PendientePago` bloquea el horario. Un turno online en proceso de pago reserva el slot hasta que se confirme o cancele.

---

## Origen (`OrigenTurno`)

Indica por qué canal fue creado el turno. Persistido como `int`.

| Valor | Nombre          | Descripción                                      |
|-------|-----------------|--------------------------------------------------|
| `0`   | `Web`           | Reserva realizada por el cliente desde la web pública (`/reservar`) |
| `1`   | `Administrativo`| Creado desde el panel de admin                   |
| `2`   | `Profesional`   | Creado por el profesional desde su vista         |
| `3`   | `Caja`          | Creado al momento del cobro en caja              |
| `4`   | `Manual`        | Carga manual retroactiva                         |

---

## Lógica de creación (`TurnoService.CreateAsync`)

Archivo: `src/Application/Services/TurnoService.cs`

1. Valida que el `Servicio` y el `Cliente` existan.
2. Calcula `FechaHoraFin = FechaHoraInicio + Servicio.DuracionAgendaMin`.
3. Verifica conflicto de horario con `ExisteConflictoHorarioAsync` (lanza `InvalidOperationException` → HTTP 409 si hay superposición).
4. Calcula `MontoSena` si el servicio lo requiere:
   - `TipoSena.Fija` → usa `Servicio.ValorSena` directamente
   - `TipoSena.Porcentaje` → `Servicio.Precio * ValorSena / 100`
5. Determina el **estado inicial** según origen y configuración:
   - `Web + RequiereSena == true` → `PendientePago` (flujo MercadoPago)
   - `Web + modo Manual` → `Pendiente`
   - Resto → `Confirmado`
6. Genera un `TokenGestion` único (`Guid.NewGuid().ToString("N")`).
7. Persiste y recarga con relaciones (`GetByIdConRelacionesAsync`).
8. Si el cliente tiene email registrado **y el estado no es `PendientePago`**, envía un correo de confirmación via `IEmailService`.
   - El email es no-crítico: si falla, la reserva igual se procesa.
   - Para turnos `PendientePago` (seña online), el email de confirmación **no se envía aquí**. Se envía desde `PagoTurnoService.ProcesarNotificacionPagoAsync` una vez que el pago sea aprobado por MercadoPago.

---

## Lógica de actualización (`TurnoService.UpdateEstadoAsync`)

Permite modificar: `Estado`, `Observaciones`, `FechaHoraInicio` y/o `ProfesionalId`.

- Si se cambia `FechaHoraInicio`, recalcula `FechaHoraFin` y valida el nuevo conflicto de horario (excluyendo el mismo turno del chequeo).
- Si se cambia `ProfesionalId` sin cambiar horario, actualiza solo el profesional.
- Si se cambian ambos a la vez, valida el conflicto para el nuevo profesional en el nuevo horario.

---

## Verificación de conflictos de horario

`ExisteConflictoHorarioAsync` en `src/Infrastructure/Repositories/TurnoRepository.cs`:

```csharp
t.ProfesionalId == profesionalId &&
t.Estado != CanceladoCliente && t.Estado != CanceladoStaff && t.Estado != NoShow &&
t.FechaHoraInicio < fin && t.FechaHoraFin > inicio
```

Dos turnos se superponen si sus intervalos `[inicio, fin)` se solapan.
El parámetro `excludeTurnoId` permite excluir el propio turno al reprogramar.

---

## Slots disponibles (`PublicoService.ObtenerSlotsAsync`)

Archivo: `src/Application/Services/PublicoService.cs`

Genera los horarios disponibles para la vista pública de reserva:

1. Lee el horario de atención de la sucursal (`ConfiguracionSucursal.HorariosAtencionJson`).
2. Filtra usuarios activos con rol `Profesional` o `Administrativo`.
3. Carga los turnos activos del día con `GetActivosSucursalEnRangoAsync`.
4. Por cada cursor de tiempo (en pasos de `DuracionAgendaMin + BufferEntreTurnos`):
   - Aplica la **anticipación mínima** (ver abajo) para descartar slots no reservables.
   - Determina qué profesionales están libres en ese slot.
   - Retorna **un `SlotDisponibleDto` por profesional libre** (no solo el primero).

```csharp
public class SlotDisponibleDto {
    string Hora;            // "HH:mm"
    string ProfesionalId;
    string ProfesionalNombre;
}
```

### Restricción de anticipación mínima

La configuración `MinutosAnticipacionMinimaReserva` de `ConfiguracionSucursal` define cuánto tiempo antes del inicio de un slot el cliente debe reservar. El filtro aplicado es:

```csharp
int anticipacionMinutos = config?.MinutosAnticipacionMinimaReserva ?? 60;

if (slotInicio <= now.AddMinutes(anticipacionMinutos))
    continue; // slot descartado
```

**Comportamiento importante:**
- Este chequeo **reemplaza** al anterior `if (esHoy && slotInicio <= now)`. Al usar `now + anticipación`, los slots pasados siempre caen dentro del rango y se descartan automáticamente, sin necesidad de la variable `esHoy`.
- Con anticipación `>= 1440` minutos (24 horas), la restricción puede afectar slots del día siguiente o posteriores. La lógica es correcta en todos los casos porque opera sobre `DateTime` absolutos.
- Esta restricción **solo aplica al flujo público** (`/api/publico/.../slots`). El staff puede crear turnos en cualquier horario desde el panel admin sin esta limitación.

### Restricción de días máximos de reserva futura

El campo `DiasMaximosReservaFutura` de `ConfiguracionSucursal` se expone en el perfil público (ver sección Perfil público). El backend **no valida** este límite en la creación del turno; la restricción se aplica en el frontend deshabilitando fechas en el calendario.

---

## Perfil público (`PublicoService.ObtenerPerfilAsync`)

Archivo: `src/Application/Services/PublicoService.cs`
Endpoint: `GET /api/publico/{sucursalId}/perfil`

Devuelve información de la sucursal visible para clientes sin autenticación. Incluye desde la iteración actual:

| Campo                      | Tipo                      | Descripción                                             |
|----------------------------|---------------------------|---------------------------------------------------------|
| `Nombre`                   | `string`                  | Nombre del negocio                                      |
| `Slogan`                   | `string?`                 | Frase descriptiva                                       |
| `FotoUrl`                  | `string?`                 | URL del logo                                            |
| `Rubros`                   | `List<string>`            | Categorías de servicios                                 |
| `Moneda`                   | `string`                  | Código de moneda (ej: `"ARS"`)                          |
| `Direccion`                | `string`                  | Dirección física                                        |
| `Telefono`                 | `string`                  | Teléfono de contacto                                    |
| `Email`                    | `string`                  | Email de contacto                                       |
| `DiasMaximosReservaFutura` | `int`                     | Máximo de días a futuro para reservar (default: `30`)   |
| `HorariosAtencion`         | `Dictionary<string, ...>` | Horarios por día de la semana                           |

El valor de `DiasMaximosReservaFutura` se lee desde `ConfiguracionSucursal.DiasMaximosReservaFutura`, con fallback a `30` si la configuración no existe.

---

## Endpoints REST

Definidos en `src/Api/Controllers/TurnosController.cs`. Todos requieren autenticación JWT excepto `by-token`.

| Método   | Ruta                          | Descripción                           | Auth |
|----------|-------------------------------|---------------------------------------|------|
| `GET`    | `/api/turnos`                 | Lista todos (filtro `?sucursalId=`)   | Sí   |
| `GET`    | `/api/turnos/{id}`            | Obtiene turno por ID con relaciones   | Sí   |
| `GET`    | `/api/turnos/by-token/{token}`| Obtiene turno por TokenGestion        | No   |
| `POST`   | `/api/turnos`                 | Crea un nuevo turno                   | Sí   |
| `PUT`    | `/api/turnos/{id}`            | Actualiza estado / reprograma         | Sí   |
| `DELETE` | `/api/turnos/{id}`            | Elimina el turno                      | Sí   |

Endpoint público de slots (en `PublicoController`):

| Método | Ruta                                            | Descripción                   |
|--------|-------------------------------------------------|-------------------------------|
| `GET`  | `/api/publico/{sucursalId}/slots?fecha=&servicioId=` | Horarios disponibles    |

---

## Vistas de frontend

### Reserva pública — `BookingFlow.tsx`

Flujo de 5 pasos (6 si el servicio requiere seña):

1. Elegir servicio
2. Elegir fecha y horario (con filtro de profesional opcional)
3. Ingresar datos del cliente
4. *(Opcional)* Abonar seña
5. Confirmación

**Al montar el componente** se cargan en paralelo:
- `serviciosApi.listar(sucursalId)` — lista de servicios activos
- `publicoApi.perfil(sucursalId)` — perfil público, del cual se extrae `diasMaximosReservaFutura`

En el paso 2 (calendario):
- Las fechas **pasadas** (`date < today`) están deshabilitadas.
- Las fechas **más allá del límite** (`date > today + diasMaximosReservaFutura`) también están deshabilitadas. Este límite se lee del perfil público y tiene default `30` días.
- Se muestran todos los profesionales libres para cada hora.
- En modo "Cualquiera" se muestra el nombre del profesional asignado en cada botón.
- Si hay conflicto al confirmar (HTTP 409), se refrescan los slots y el usuario vuelve al paso 2.

> **Staff vs. cliente**: El drawer `NuevoTurnoDrawer` usado por el staff internamente usa un `<input type="date">` libre sin consumir el perfil público. Las restricciones de anticipación mínima y días máximos **no aplican al staff**.

### Gestión admin — `AppointmentDetail.tsx`

Vista detallada de un turno con pestañas:
- **Info**: datos del cliente, servicio, profesional, observaciones
- **Pagos**: historial de cobros
- **Comentarios**: notas internas
- **Adjuntos**: archivos asociados

Acciones disponibles:
- Marcar como **Confirmado**
- Marcar como **Completado**
- Marcar como **CanceladoStaff**

### Agenda — `Agenda.tsx`

Vista de calendario con dos modos:
- **Diaria**: columnas por profesional, filas por hora
- **Semanal**: columnas por día, filas por hora

Los turnos se posicionan absolutamente según `FechaHoraInicio` y duración.
Turnos superpuestos en la misma columna se dividen en lanes horizontales.

Los estados del conjunto `ESTADOS_GHOST` se renderizan en modo opaco y se ocultan por defecto, mostrándose solo al activar el botón "Ver cancelados":

```ts
const ESTADOS_GHOST = new Set(['CanceladoStaff', 'CanceladoCliente', 'NoShow', 'PagoRechazado']);
```

`PagoRechazado` se agrupa con los cancelados porque representa un turno que nunca se concretó, igual que una cancelación. Esto evita que ocupe espacio visual en la agenda principal.

### Vista pública del cliente — `TurnoToken.tsx`

Accesible sin login via `/turno/:token`. Permite al cliente:
- Ver el detalle de su turno
- Cancelar (cambia estado a `CanceladoCliente`)

---

## Notificaciones por email

Implementado en `src/Infrastructure/Services/EmailService.cs` usando **MailKit** (SMTP).

| Trigger | Template | Destinatario |
|---|---|---|
| Turno creado con estado `Confirmado` o `Pendiente` (si tiene email) | Confirmación de turno | Cliente |
| Webhook MP `payment.approved` — turno `PendientePago` pasa a `Confirmado` | Confirmación de turno | Cliente |
| Staff cancela turno (`CanceladoStaff`) | Cancelación por staff | Cliente |
| Cliente cancela turno (`CanceladoCliente`) | Cancelación por cliente | Cliente |
| Cliente cancela turno (`CanceladoCliente`) | Alerta liberación de agenda | Profesional (o email de sucursal) |

> **Regla clave:** los turnos creados con `estado = PendientePago` **no reciben email en la creación**. El email de confirmación se envía únicamente cuando el pago es aprobado por MercadoPago (vía webhook). Esto evita que el cliente reciba un "turno confirmado" antes de haber pagado.

Configuración SMTP en `appsettings.json`:
```json
"Smtp": {
  "Host": "smtp.gmail.com",
  "Port": "587",
  "Username": "...",
  "Password": "...",
  "FromName": "TurneroApp"
}
```

En entorno `Development` sin SMTP configurado se usa `ConsoleEmailService` que imprime el email en la consola.

---

## Índices de base de datos

Definidos en `TurnoConfiguration.cs`:

| Índice                             | Tipo   | Uso                                       |
|------------------------------------|--------|-------------------------------------------|
| `(ProfesionalId, FechaHoraInicio)` | Normal | Consultas de agenda por profesional       |
| `TokenGestion`                     | Único  | Lookup rápido por token público           |
| `(SucursalId, FechaHoraInicio)`    | Normal | Consultas de agenda por sucursal y fecha  |
