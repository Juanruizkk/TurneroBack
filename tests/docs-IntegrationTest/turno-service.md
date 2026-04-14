# TurnoServiceIntegrationTests

Tests de integración para `TurnoService.CreateAsync`.

Verifican que la creación de turnos interactúa correctamente con la base de datos real: límite mensual del plan, detección de conflictos de horario y persistencia completa de campos.

---

## Servicio bajo test

`TurnoService.CreateAsync(CreateTurnoDto dto)`

Construido con `ServiceFactory.BuildTurnoService(db)`, que usa repositorios reales y mockea únicamente los side-effects externos (email y notificaciones SignalR).

---

## Setup común

Todos los tests comparten el mismo patrón de seed base:

```
Plan → Sucursal → SucursalSuscripcion
                → Cliente
                → Servicio
Usuario (profesional)
→ CreateTurnoDto { SucursalId, ClienteId, ServicioId, ProfesionalId, FechaHoraInicio }
```

**Nota sobre `horarioRepo` y `usuarioServicioRepo`:** se omiten al construir el servicio para no activar la validación de horario laboral del profesional ni la de servicios asignados, que son ortogonales a los escenarios aquí testeados.

---

## Tests

### Test 1 — Límite mensual del plan

```
CreateAsync_CuandoLimiteMensualAlcanzado_LanzaInvalidOperationException
```

**Escenario:** El plan tiene `MaxTurnosMes = 2` y ya existen 2 turnos en el mes actual. Se intenta crear un 3er turno.

**Arrange:**
- `Plan { MaxTurnosMes = 2 }`
- Suscripción activa (`PruebActiva`, vence en 30 días)
- 2 turnos ya persistidos en la DB para el mes en curso (día 5, a las 10:00 y 12:00 UTC)

**Act:**
```csharp
sut.CreateAsync(dto) // dto apunta al día 5 a las 15:00
```

**Assert:**
- Lanza `InvalidOperationException` con mensaje `*Actualizá tu suscripción*`
- `Db.Turnos.Count(t => t.SucursalId == sucursal.Id) == 2` (el 3er no se persistió)

---

### Test 2 — Conflicto de horario

```
CreateAsync_CuandoConflictoHorario_LanzaInvalidOperationException
```

**Escenario:** Existe un turno de 10:00 a 10:30. Se intenta crear otro turno a las 10:15 para el mismo profesional (solapa con el existente).

**Arrange:**
- Plan sin límite de turnos (`maxTurnosMes: null`)
- Turno existente en DB: `2026-06-10 10:00 → 10:30` (30 min, misma sucursal y profesional)
- Nuevo dto: `FechaHoraInicio = 10:15`

**Detección de conflicto en el repositorio:**
```sql
WHERE profesional_id = @profId
  AND fecha_hora_inicio < @fin      -- 10:45
  AND fecha_hora_fin   > @inicio    -- 10:15
  AND estado NOT IN ('CanceladoCliente', 'CanceladoStaff', 'NoShow', 'PagoRechazado')
```

**Assert:**
- Lanza `InvalidOperationException` con mensaje `*ya tiene un turno en el horario seleccionado*`
- `Db.Turnos.Count(t => t.SucursalId == sucursal.Id) == 1` (el conflictivo no se persistió)

---

### Test 3 — Happy path: persistencia real en PostgreSQL

```
CreateAsync_CuandoDatosValidos_PersisteTurnoConCamposCorrectos
```

**Escenario:** Todos los datos son válidos. Se verifica que el turno se crea correctamente y los campos críticos se persisten con los valores esperados.

**Arrange:**
- Plan sin límite, suscripción activa
- Servicio con `DuracionAgendaMin = 45`
- `FechaHoraInicio = 2026-07-20 09:00 UTC`

**Act:**
```csharp
var result = await sut.CreateAsync(dto);
```

**Assert sobre el DTO de respuesta:**
- `result != null`
- `result.ClienteNombre` no vacío

**Assert sobre la DB (persistencia real):**

| Campo | Valor esperado | Cómo se calcula |
|---|---|---|
| `ClienteId` | `cliente.Id` | Tomado directamente del DTO |
| `ServicioId` | `servicio.Id` | Tomado directamente del DTO |
| `ProfesionalId` | `prof.Id` | Tomado directamente del DTO |
| `FechaHoraFin` | `09:45 UTC` | `FechaHoraInicio + DuracionAgendaMin (45)` |
| `TokenGestion` | Non-empty string | `Guid.NewGuid().ToString("N")` generado en el servicio |

---

## Qué NO testean estos tests

| Escenario | Por qué se omite |
|---|---|
| Validación de horario laboral del profesional | Ortogonal; `horarioRepo` se omite al construir el servicio. Ver `SlotsDisponiblesIntegrationTests`. |
| Validación de servicios asignados al profesional | Ídem anterior. |
| Estado inicial del turno (Pendiente vs Confirmado vs PendientePago) | Depende de `ModoConfirmacionTurnos` y `RequiereSena`; cubierto en tests unitarios. |
| Envío de email de confirmación | Side-effect externo; mockeado. |
| Notificación SignalR | Side-effect externo; mockeado. |
