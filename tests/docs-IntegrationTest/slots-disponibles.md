# SlotsDisponiblesIntegrationTests

Tests de integración para `PublicoService.ObtenerSlotsAsync`.

Verifican que el algoritmo de generación de slots interactúa correctamente con la base de datos real: horarios de atención, conflictos de turno, duración del servicio, buffer entre turnos y horario laboral del profesional.

---

## Servicio bajo test

`PublicoService.ObtenerSlotsAsync(string slug, DateOnly fecha, Guid servicioId)`

Construido con `ServiceFactory.BuildPublicoService(db)`, que usa repositorios reales contra el contenedor PostgreSQL de Testcontainers.

---

## Constantes de fecha

| Constante | Valor | Por qué |
|---|---|---|
| `LunesFuturo` | `2027-03-01` | Lunes → abierto en horario por defecto. Fecha futura → todos los slots pasan el filtro de anticipación mínima. |
| `DomingoCerrado` | `2027-02-28` | Domingo → cerrado en el horario por defecto del sistema. |

> El servicio usa `DateTime.Now` para filtrar slots pasados. Usar fechas en 2027 garantiza que los tests no se rompan cuando pase el tiempo.

---

## Setup común

La mayoría de los tests usa un `HorariosAtencionJson` personalizado (via `SeedHelper.HorarioJson`) en lugar del horario por defecto, para tener un conteo de slots determinístico y asserts precisos.

**Orden de seed requerido:**
```
Sucursal
  └── ConfiguracionSucursal   ← horario, buffer, anticipación
  └── Servicio                ← duración que define el paso del cursor
  └── Usuario (prof)
        └── UsuarioSucursalRol ← vincula el prof a la sucursal como Profesional activo
              └── HorarioLaboral (opcional, solo test 6)
```

---

## Tests

### Test 1 — Happy path: slots por cada profesional

```
ObtenerSlotsAsync_SinTurnosNiConflictos_RetornaSlotsPorCadaProfesional
```

**Escenario:** 2 profesionales activos, sin turnos existentes, franja 09:00-11:00 con servicio de 30 min.

**Arrange:**
- Horario: Lunes 09:00-11:00
- Servicio: 30 min de duración
- 2 profesionales activos en la sucursal (sin turnos)

**Lógica esperada del cursor:**
```
cursor=09:00 → 09:00+30=09:30 ≤ 11:00 → slot ✓
cursor=09:30 → 09:30+30=10:00 ≤ 11:00 → slot ✓
cursor=10:00 → 10:00+30=10:30 ≤ 11:00 → slot ✓
cursor=10:30 → 10:30+30=11:00 ≤ 11:00 → slot ✓
cursor=11:00 → 11:00+30=11:30 > 11:00 → stop
```

**Assert:**
- Total: 8 slots (4 slots × 2 profesionales)
- Horas distintas: `["09:00", "09:30", "10:00", "10:30"]`
- IDs de profesional distintos: 2

---

### Test 2 — Turno existente bloquea el slot del profesional ocupado

```
ObtenerSlotsAsync_ConTurnoQueBloquea_ExcluyeSlotDelProfesionalOcupado
```

**Escenario:** Un profesional tiene un turno confirmado a las 09:00-09:30. Ese slot no debe aparecer para él; los posteriores sí.

**Arrange:**
- Horario: Lunes 09:00-10:30
- Servicio: 30 min
- 1 profesional activo
- Turno existente: `2027-03-01 09:00 → 09:30` (mismo prof, misma sucursal)

**Cómo detecta el conflicto:**
`PublicoService` carga `GetActivosSucursalEnRangoAsync` y verifica:
```
turno.FechaHoraInicio < slotFin AND turno.FechaHoraFin > slotInicio
```

**Assert:**
- El slot `"09:00"` para ese profesional **no** aparece en los resultados
- Los slots `"09:30"` y `"10:00"` **sí** aparecen

---

### Test 3 — Duración larga reduce la cantidad de slots

```
ObtenerSlotsAsync_DuracionServicioLarga_ReduceSlotsDisponibles
```

**Escenario:** Franja de 90 minutos (09:00-10:30) con un servicio de 60 min. Solo cabe un slot.

**Arrange:**
- Horario: Lunes 09:00-10:30
- Servicio: 60 min de duración
- 1 profesional activo

**Lógica esperada del cursor:**
```
cursor=09:00 → 09:00+60=10:00 ≤ 10:30 → slot ✓
cursor=10:00 → 10:00+60=11:00 > 10:30 → stop
```

**Assert:**
- Total: 1 slot
- Hora: `"09:00"`

---

### Test 4 — Buffer entre turnos espacía los slots

```
ObtenerSlotsAsync_ConBuffer_AgregaEspacioEntreSlots
```

**Escenario:** Con un buffer de 15 minutos entre turnos, el cursor avanza `duración + buffer` en cada paso, resultando en menos slots con mayor separación.

**Arrange:**
- Horario: Lunes 09:00-10:30
- Servicio: 30 min
- Buffer: 15 min (`ConfiguracionSucursal.MinutosBufferEntreTurnos = 15`)
- 1 profesional activo

**Lógica esperada del cursor (sin buffer vs con buffer):**
```
Sin buffer:  09:00 → 09:30 → 10:00            (3 slots)
Con buffer:  09:00 → 09:45 → 10:30 (no cabe)  (2 slots)
```

**Assert:**
- Total: 2 slots
- Horas: `["09:00", "09:45"]`
- `"09:30"` **no** aparece (sería sin buffer)

---

### Test 5 — Día cerrado retorna lista vacía

```
ObtenerSlotsAsync_DiaCerrado_RetornaListaVacia
```

**Escenario:** Se consultan slots para un domingo, que está cerrado en el horario por defecto del sistema.

**Arrange:**
- Sin `ConfiguracionSucursal` → el servicio usa `AjustesService.HorariosDefault`
- El horario por defecto tiene Domingo sin franjas abiertas
- 1 profesional activo (no importa, el horario cierra antes)
- Fecha: `2027-02-28` (domingo)

**Assert:**
- Lista vacía

---

### Test 6 — HorarioLaboral del profesional filtra slots fuera de su rango

```
ObtenerSlotsAsync_HorarioLaboralConfigurado_ExcluyeSlotsFueraDeRangoDelProfesional
```

**Escenario:** La sucursal abre de 09:00 a 13:00, pero el profesional solo trabaja de 12:00 a 13:00 ese día. Solo deben aparecer slots dentro del horario laboral del profesional.

**Arrange:**
- Horario de sucursal: Lunes 09:00-13:00
- Servicio: 30 min
- 1 profesional activo
- `HorarioLaboral` del profesional: Lunes `HoraDesde=12:00`, `HoraHasta=13:00`

**Cómo filtra el servicio:**
```csharp
if (horariosPorProf.TryGetValue(profId, out var horarios) && horarios.Count > 0)
{
    if (!horarios.Any(h =>
            h.Activo &&
            h.DiaSemana == fecha.DayOfWeek &&
            h.HoraDesde <= slotHoraInicio &&
            h.HoraHasta >= slotHoraFin))
        return false; // excluye al prof de este slot
}
```

**Evaluación slot por slot:**

| Slot | slotInicio | slotFin | HoraDesde ≤ Inicio? | HoraHasta ≥ Fin? | Resultado |
|---|---|---|---|---|---|
| 09:00 | 09:00 | 09:30 | 12:00 ≤ 09:00 → ✗ | — | Excluido |
| 09:30 | 09:30 | 10:00 | 12:00 ≤ 09:30 → ✗ | — | Excluido |
| ... | ... | ... | ... | ... | Excluido |
| 12:00 | 12:00 | 12:30 | 12:00 ≤ 12:00 → ✓ | 13:00 ≥ 12:30 → ✓ | **Incluido** |
| 12:30 | 12:30 | 13:00 | 12:00 ≤ 12:30 → ✓ | 13:00 ≥ 13:00 → ✓ | **Incluido** |

**Assert:**
- Total: 2 slots
- Horas: `["12:00", "12:30"]`
- ProfesionalId: el del prof seeded

---

## Qué NO testean estos tests

| Escenario | Por qué se omite |
|---|---|
| Filtro de servicios asignados al profesional (`UsuarioServicio`) | Capa separada, cubre un único concern. Requeriría seed de `UsuarioServicio`. |
| Múltiples franjas horarias en el mismo día | El horario por defecto lo cubre implícitamente en test 1 (pero con franja única para mayor control). |
| `DiasMaximosReservaFutura` | Ortogonal al algoritmo de slots; es una validación de frontend. |
| Slug inválido / sucursal no encontrada | Retorna `[]` por contrato del servicio; no requiere test de integración. |
