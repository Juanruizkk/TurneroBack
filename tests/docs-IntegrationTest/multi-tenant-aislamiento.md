# MultiTenantAislamientoIntegrationTests

Tests de integración que verifican el aislamiento multi-tenant del sistema.

En TurneroApp, el eje central es `Sucursal`: cada sucursal es completamente independiente y sus datos (clientes, servicios, turnos) no deben filtrarse hacia otras sucursales. El aislamiento se implementa mediante filtros explícitos `WHERE sucursal_id = ?` en cada método de repositorio (no hay global query filters para esto).

Estos tests verifican que ese aislamiento se cumple en la DB real.

---

## Qué se testea

| Capa | Método bajo test | Propiedad verificada |
|---|---|---|
| Repositorio | `ClienteRepository.BuscarPorTelefonoAsync` | Un cliente de la sucursal A no aparece al buscar en B |
| Repositorio | `ClienteRepository.BuscarPorTelefonoAsync` | Mismo teléfono en A y B → cada búsqueda retorna el cliente de su sucursal |
| Repositorio | `TurnoRepository.GetActivosSucursalEnRangoAsync` | Turnos de A no aparecen en el rango de B |
| Repositorio | `ServicioRepository.GetBySucursalAsync` | Servicios de A no aparecen en el listado de B |
| Servicio | `PublicoService.ObtenerSlotsAsync` | Turnos de la sucursal A no generan conflicto en los slots de B |

---

## Setup común

Todos los tests crean dos sucursales independientes (A y B) con `SeedHelper.SeedSucursalAsync`. La separación es por `Guid` de sucursal; no existe ningún recurso compartido entre ellas (salvo en el test 4, donde un mismo `Usuario` es profesional en ambas).

---

## Tests

### Test 1 — Cliente de otra sucursal no aparece en búsqueda por teléfono

```
BuscarClientePorTelefono_NoDevuelveClienteDeOtraSucursal
```

**Escenario:** El teléfono `"3814000001"` existe como cliente en la sucursal A. Al buscarlo con el ID de la sucursal B, el resultado debe ser `null`.

**Arrange:**
- Sucursal A y sucursal B creadas
- `Cliente { Telefono = "3814000001", SucursalId = A }` persistido directamente en DB

**Act:**
```csharp
repo.BuscarPorTelefonoAsync(sucursalB.Id, "3814000001")
```

**Assert:**
- Resultado: `null`

**Por qué importa:** Sin este filtro, un cliente podría ser encontrado y reutilizado en otra sucursal sin que esa sucursal lo haya registrado, contaminando el historial de clientes y potencialmente exponiendo datos personales.

---

### Test 2 — Mismo teléfono en dos sucursales retorna el cliente correcto

```
BuscarClientePorTelefono_DevuelveClienteDeLaSucursalCorrecta
```

**Escenario:** El teléfono `"3814000002"` está registrado tanto en la sucursal A como en la B (caso real: el mismo cliente va a dos negocios distintos). Cada sucursal debe obtener su propio registro.

**Arrange:**
- Sucursal A: `Cliente { Id = A1, Telefono = "3814000002", SucursalId = A }`
- Sucursal B: `Cliente { Id = B1, Telefono = "3814000002", SucursalId = B }`

**Act:**
```csharp
var resultadoA = repo.BuscarPorTelefonoAsync(sucursalA.Id, "3814000002")
var resultadoB = repo.BuscarPorTelefonoAsync(sucursalB.Id, "3814000002")
```

**Assert:**
- `resultadoA.Id == A1`
- `resultadoB.Id == B1`

**Por qué importa:** Valida que el filtro no solo devuelve `null` sino que devuelve el registro *correcto* cuando hay homónimos en varias sucursales.

---

### Test 3 — Turnos de otra sucursal no aparecen en el rango

```
GetTurnosPorRango_NoDevuelveTurnosDeOtraSucursal
```

**Escenario:** La sucursal A tiene un turno el `2027-03-01 10:00`. Al consultar el mismo rango de fechas pero con el ID de la sucursal B, el resultado debe estar vacío.

**Arrange:**
- Sucursal A con turno en `2027-03-01 10:00`
- Sucursal B sin ningún turno

**Act:**
```csharp
repo.GetActivosSucursalEnRangoAsync(
    sucursalB.Id,
    new DateTime(2027, 3, 1, 0, 0, 0),
    new DateTime(2027, 3, 1, 23, 59, 59))
```

**Assert:**
- Lista vacía

**Por qué importa:** `GetActivosSucursalEnRangoAsync` es el método que usa `PublicoService` para cargar los turnos del día antes de calcular los slots. Si no filtrara por sucursal, los turnos de otros negocios podrían bloquear slots incorrectamente.

---

### Test 4 — Servicios de otra sucursal no aparecen en el listado

```
GetServiciosBySucursal_NoDevuelveServiciosDeOtraSucursal
```

**Escenario:** La sucursal A tiene 3 servicios y la sucursal B tiene 1. Cada listado debe devolver solo los servicios propios.

**Arrange:**
- Sucursal A: 3 servicios seeded (`SeedServicioAsync` × 3)
- Sucursal B: 1 servicio seeded

**Act:**
```csharp
var serviciosA = repo.GetBySucursalAsync(sucursalA.Id)
var serviciosB = repo.GetBySucursalAsync(sucursalB.Id)
```

**Assert:**
- `serviciosA` tiene exactamente 3 elementos
- `serviciosB` tiene exactamente 1 elemento
- `serviciosB.Single().Id == servicioB.Id`

**Por qué importa:** Un cliente web que visita `/s/negocio-b` no debe ver ni poder seleccionar servicios de otro negocio.

---

### Test 5 — Turnos de otra sucursal no generan conflicto en los slots

```
ObtenerSlots_TurnosDeOtraSucursalNoGeneranConflicto
```

**Escenario:** El mismo profesional trabaja en dos sucursales. La sucursal A tiene un turno para ese profesional a las 09:00. Al pedir slots para la sucursal B, el horario de las 09:00 debe aparecer disponible porque el conflicto es de la sucursal A, no de B.

**Arrange:**
```
Profesional (mismo Usuario.Id)
  ├── UsuarioSucursalRol { SucursalId = A, Rol = Profesional, Estado = Activo }
  └── UsuarioSucursalRol { SucursalId = B, Rol = Profesional, Estado = Activo }

Turno en sucursal A: 2027-03-01 09:00 → 09:30 (prof bloqueado en A)
```

**Act:**
```csharp
// Pedir slots para B → no debe ver el turno de A
var slotsB = sut.ObtenerSlotsAsync(sucursalB.Slug, fecha, servicioB.Id)

// Control: pedir slots para A → sí debe ver el bloqueo
var slotsA = sut.ObtenerSlotsAsync(sucursalA.Slug, fecha, servicio.Id)
```

**Flujo interno del servicio:**
```
ObtenerSlotsAsync(slug = B)
  └── GetActivosSucursalEnRangoAsync(sucursalB.Id, ...) → [] (vacío)
      ↓ sin conflictos para B
  └── slot 09:00 → profesional libre → incluido ✓

ObtenerSlotsAsync(slug = A)
  └── GetActivosSucursalEnRangoAsync(sucursalA.Id, ...) → [turno 09:00-09:30]
      ↓ conflicto detectado
  └── slot 09:00 → profesional ocupado → excluido ✗
```

**Assert:**
- `slotsB` contiene `Hora == "09:00"` → el turno de A no contamina B
- `slotsA` **no** contiene `Hora == "09:00"` → el bloqueo en A funciona correctamente

**Por qué importa:** Es el test de aislamiento más crítico a nivel de servicio. Sin el filtro de `SucursalId` en `GetActivosSucursalEnRangoAsync`, un profesional que trabaja en dos negocios distintos vería sus turnos de un negocio bloqueando los slots del otro, haciendo imposible tomar turnos en ambos lugares al mismo tiempo.

---

## Relación con la arquitectura multi-tenant

```
Nivel de aislamiento en TurneroApp:
  ┌──────────────────────────────────────────┐
  │  Todo filtrado por SucursalId explícito  │
  │  en cada método de repositorio.          │
  │                                          │
  │  NO hay:                                 │
  │  - Global query filters por tenant       │
  │  - Row Level Security en PostgreSQL      │
  │  - Schemas separados por sucursal        │
  └──────────────────────────────────────────┘
```

Esto significa que si un método de repositorio omite el filtro `SucursalId`, los datos se mezclan sin ninguna red de seguridad a nivel de infraestructura. Estos tests actúan como esa red de seguridad.

---

## Qué NO testean estos tests

| Escenario | Por qué se omite |
|---|---|
| Aislamiento en `CajaController` (movimientos de caja) | Se prioriza la capa de datos más crítica al flujo de reservas |
| Acceso de un JWT de la sucursal A a endpoints de la sucursal B | Eso es aislamiento de autorización (tests de HTTP); estos son tests de capa de datos |
| `UsuarioSucursalRol` aislado por sucursal | Implícitamente verificado en el test 5 (un mismo usuario aparece en ambas) |
