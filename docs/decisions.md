# API — Documentación y decisiones técnicas

Sistema de gestión de turnos para estéticas y peluquerías.
Base URL local: `http://localhost:5000/api`
Swagger UI: `http://localhost:5000/swagger`

---

## Índice

1. [Convenciones generales](#1-convenciones-generales)
2. [Sucursales](#2-sucursales)
3. [Servicios](#3-servicios)
4. [Clientes](#4-clientes)
5. [Turnos](#5-turnos)
6. [Caja](#6-caja)
7. [Enums de referencia](#7-enums-de-referencia)
8. [Decisiones técnicas](#8-decisiones-técnicas)
9. [Flujo operativo típico](#9-flujo-operativo-típico)

---

## 1. Convenciones generales

- Todos los IDs son `Guid` (UUID v4).
- Todos los montos son `decimal`.
- Fechas en formato ISO 8601: `"2025-06-15T10:30:00"`.
- Los enums se envían y reciben como **string** (ej: `"Pendiente"`, `"Efectivo"`).
- Errores de negocio devuelven `{ "message": "descripción del error" }`.
- `201 Created` incluye el header `Location` apuntando al recurso creado.

---

## 2. Sucursales

**Base:** `GET|POST /api/sucursales` · `GET|PUT|DELETE /api/sucursales/{id}`

Cada sucursal es una unidad operativa independiente. Todos los demás recursos (usuarios, servicios, clientes, turnos, caja) pertenecen a una sucursal.

### POST /api/sucursales
```json
{
  "nombre": "Sede Centro",
  "direccion": "Av. Corrientes 1234",
  "telefono": "1123456789",
  "email": "centro@ejemplo.com"
}
```
La sucursal se crea siempre con `activa: true`.

### GET /api/sucursales
```
GET /api/sucursales              → todas
GET /api/sucursales?soloActivas=true  → solo activas
GET /api/sucursales/{id}         → una
```

### PUT /api/sucursales/{id}
Igual que el Create pero incluye `"activa": true|false` para activar/desactivar.

### Response
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "nombre": "Sede Centro",
  "direccion": "Av. Corrientes 1234",
  "telefono": "1123456789",
  "email": "centro@ejemplo.com",
  "activa": true
}
```

---

## 3. Servicios

**Base:** `GET|POST /api/servicios` · `GET|PUT|DELETE /api/servicios/{id}`

### POST /api/servicios
```json
{
  "sucursalId": "...",
  "nombre": "Corte de cabello",
  "descripcion": "Corte con tijera o máquina",
  "aclaraciones": "Incluye lavado",
  "precio": 5000.00,
  "duracionClienteMin": 30,
  "duracionAgendaMin": 30,
  "requiereSena": false,
  "tipoSena": null,
  "valorSena": null,
  "modalidadPrestacion": "Presencial",
  "tipoServicio": "Regular"
}
```

**Dos tiempos distintos:**
- `duracionClienteMin`: lo que percibe el cliente (se muestra en la reserva pública).
- `duracionAgendaMin`: lo que bloquea en el calendario del profesional. Este valor es el que usa el motor al calcular `FechaHoraFin`.

**Seña:**
Si `requiereSena: true`, deben enviarse `tipoSena` y `valorSena`:
- `tipoSena: "Fija"` → `valorSena` es el monto fijo (ej: `2000`).
- `tipoSena: "Porcentaje"` → `valorSena` es el porcentaje sobre el precio (ej: `30` = 30%).

### GET /api/servicios
```
GET /api/servicios                                          → todos (solo activos)
GET /api/servicios?sucursalId={id}                         → por sucursal (solo activos)
GET /api/servicios?sucursalId={id}&incluirInactivos=true   → por sucursal (activos e inactivos)
GET /api/servicios/{id}                                    → uno
```

> `incluirInactivos=false` es el default. El panel admin usa `incluirInactivos=true`; el booking flow y el staff usan el default.

---

## 4. Clientes

**Base:** `GET|POST /api/clientes` · `GET|PUT|DELETE /api/clientes/{id}`

Los clientes pertenecen a una sucursal. No se identifican por email sino por teléfono (o combinación nombre+teléfono).

### POST /api/clientes
```json
{
  "sucursalId": "...",
  "nombre": "Juan",
  "apellido": "Pérez",
  "telefono": "1145678901",
  "email": "juan@ejemplo.com",
  "observaciones": "Alérgico a ciertos productos"
}
```
`email` y `observaciones` son opcionales.

### GET /api/clientes — Búsqueda
El endpoint de listado soporta búsqueda por teléfono o email dentro de una sucursal:

```
GET /api/clientes?sucursalId={id}&telefono=1145678901
GET /api/clientes?sucursalId={id}&email=juan@ejemplo.com
GET /api/clientes?sucursalId={id}   → todos los de la sucursal
```

Retorna siempre un array. Si no encuentra por teléfono/email, retorna `[]`.

**Flujo recomendado al crear un turno:**
Buscar primero por teléfono → si existe, usar ese ID; si no, crear el cliente y usar el nuevo ID.

---

## 5. Turnos

**Base:** `GET|POST /api/turnos` · `GET|PUT|DELETE /api/turnos/{id}`
**Adicional:** `GET /api/turnos/by-token/{token}`

### POST /api/turnos

Solo se envía `FechaHoraInicio`. La API calcula `FechaHoraFin` sumando `Servicio.DuracionAgendaMin`.

```json
{
  "sucursalId": "...",
  "clienteId": "...",
  "servicioId": "...",
  "profesionalId": "...",
  "fechaHoraInicio": "2025-06-15T10:00:00",
  "origen": "Administrativo",
  "observaciones": "El cliente prefiere la silla del fondo"
}
```

**Validación de conflicto:**
Antes de crear, el sistema verifica que el profesional no tenga otro turno que se superponga con el intervalo `[FechaHoraInicio, FechaHoraFin)`. Si hay conflicto: `409 Conflict`.

**Cálculos automáticos al crear:**
- `fechaHoraFin = fechaHoraInicio + servicio.duracionAgendaMin`
- `precioServicio` = precio actual del servicio (se guarda como snapshot)
- `montoSena` = calculado según configuración del servicio (puede ser null si no requiere seña)
- `totalCobrado = 0` (se actualiza al registrar pagos)
- `tokenGestion` = Guid aleatorio (32 caracteres hex, sin guiones)
- `estado = Pendiente`

### Response de turno
```json
{
  "id": "...",
  "sucursalId": "...",
  "clienteId": "...",
  "clienteNombre": "Juan Pérez",
  "clienteTelefono": "1145678901",
  "servicioId": "...",
  "servicioNombre": "Corte de cabello",
  "profesionalId": "...",
  "profesionalNombre": "María García",
  "fechaHoraInicio": "2025-06-15T10:00:00",
  "fechaHoraFin": "2025-06-15T10:30:00",
  "estado": "Pendiente",
  "origen": "Administrativo",
  "observaciones": null,
  "tokenGestion": "a1b2c3d4e5f6...",
  "precioServicio": 5000.00,
  "montoSena": null,
  "totalCobrado": 0.00
}
```

### PUT /api/turnos/{id} — Actualizar estado o reprogramar

```json
{
  "estado": "Confirmado",
  "observaciones": "Confirmado por WhatsApp",
  "fechaHoraInicio": "2025-06-15T11:00:00",
  "profesionalId": null
}
```

- `estado` es **requerido**.
- `fechaHoraInicio` y `profesionalId` son opcionales. Si se envía `fechaHoraInicio`, se recalcula `FechaHoraFin` y se valida conflicto.
- Si solo se envía `profesionalId` sin nueva fecha, cambia el profesional sin mover el horario.

**Estados válidos para transición manual:**

| Estado | Descripción |
|--------|-------------|
| `Pendiente` | Recién creado, sin confirmar |
| `Confirmado` | Confirmado por el local o el cliente |
| `CanceladoCliente` | Cancelado por el cliente |
| `CanceladoStaff` | Cancelado por el staff |
| `Completado` | Atendido exitosamente |
| `NoShow` | El cliente no se presentó |
| `Reprogramado` | Fue movido a otro horario |

### GET /api/turnos/by-token/{token}

Permite al cliente consultar su turno sin autenticación usando el token recibido al reservar.

```
GET /api/turnos/by-token/a1b2c3d4e5f6...
```

### GET /api/turnos
```
GET /api/turnos                      → todos
GET /api/turnos?sucursalId={id}      → por sucursal
```

---

## 6. Caja

Cada sucursal tiene exactamente **una caja**. Los movimientos registran todos los ingresos y egresos.

### GET /api/caja/sucursal/{sucursalId}
Retorna los datos de la caja de esa sucursal.

```json
{
  "id": "...",
  "sucursalId": "...",
  "nombre": "Caja principal",
  "activa": true
}
```

### GET /api/caja/{cajaId}/movimientos
```
GET /api/caja/{cajaId}/movimientos
GET /api/caja/{cajaId}/movimientos?desde=2025-06-01&hasta=2025-06-30
```

### POST /api/caja/{cajaId}/movimientos — Registrar un cobro o movimiento

```json
{
  "cajaId": "...",
  "usuarioId": "...",
  "turnoId": "...",
  "tipoMovimiento": "CobroTurno",
  "medioPago": "Efectivo",
  "monto": 5000.00,
  "observaciones": "Pago total del servicio"
}
```

- `turnoId` es opcional. Se usa cuando el movimiento está asociado a un turno (cobro de seña o saldo).
- `cajaId` en el body **debe coincidir** con el de la URL, si no retorna `400 Bad Request`.
- `usuarioId` identifica quién registró el movimiento (no quién atendió).

**Tipos de movimiento:**

| Valor | Uso |
|-------|-----|
| `CobroTurno` | Cobro asociado a un turno |
| `Gasto` | Gasto operativo del local |
| `Retiro` | Retiro de efectivo |
| `Ajuste` | Corrección manual |
| `IngresoManual` | Ingreso no vinculado a turno |

**Medios de pago:** `Efectivo` · `MercadoPago` · `Transferencia` · `Tarjeta` · `Otro`

---

## 7. Enums de referencia

### OrigenTurno
`Web` · `Administrativo` · `Profesional` · `Caja` · `Manual`

### TipoSena
`Fija` · `Porcentaje`

### ModalidadPrestacion
`Presencial` · `Maquina` · `Tratamiento` · `Otro`

### TipoServicio
`Regular` · `Variable`

> Para v1 priorizar `Regular`. `Variable` queda como extensión futura.

### ConceptoPagoTurno *(entidad PagoTurno, no expuesta aún como endpoint)*
`Sena` · `PagoParcial` · `PagoTotal` · `SaldoFinal`

---

## 8. Decisiones técnicas

### Cada sucursal es independiente
No hay recursos compartidos entre sucursales. Un cliente de la sucursal A no es el mismo que uno de la sucursal B aunque tenga el mismo teléfono. Esto simplifica el modelo en v1.

### FechaHoraFin no se envía, se calcula
El cliente de la API solo envía `FechaHoraInicio`. La API calcula `FechaHoraFin` en base a `Servicio.DuracionAgendaMin`. Esto garantiza consistencia y evita que el frontend pueda enviar duraciones arbitrarias.

### PrecioServicio se guarda como snapshot
Al crear el turno, `precioServicio` se copia del servicio en ese momento. Si el precio del servicio cambia luego, los turnos ya creados conservan el precio original.

### TokenGestion para clientes sin cuenta
El token permite a un cliente gestionar su turno (ver, cancelar, reprogramar) sin necesidad de registrarse ni autenticarse. Es un UUID de 32 caracteres hex, único en la base de datos.

### Conflicto de horario
La validación es: existe conflicto si `slotInicio < reservaFin AND slotFin > reservaInicio`. No hay conflicto solo cuando el nuevo turno empieza después de que termina el existente, o termina antes de que empiece. Al reprogramar, el turno actual se excluye de la validación.

### Seña: la política de devolución es responsabilidad del sistema, no de Mercado Pago
Mercado Pago solo ejecuta el cobro/refund. La lógica de si corresponde devolución (ej: más de 24h → devolución total) vive en el backend.

### DELETE vs cancelación (turnos)
`DELETE /api/turnos/{id}` elimina físicamente el registro. Para cancelar un turno conservando el historial, usar `PUT /api/turnos/{id}` con `estado: "CanceladoCliente"` o `"CanceladoStaff"`.

### Soft-delete de servicios
`DELETE /api/servicios/{id}` no elimina el registro: pone `Activo = false`. Esto evita errores de FK cuando el servicio tiene turnos asignados (`DeleteBehavior.Restrict`). Los turnos ya agendados se mantienen intactos y conservan el snapshot de precio. Para reactivar, usar `PUT /api/servicios/{id}` con `activo: true`.

---

## 9. Flujo operativo típico

### Crear un turno desde el sistema administrativo

```
1. GET /api/sucursales?soloActivas=true
   → obtener sucursalId

2. GET /api/servicios?sucursalId={id}
   → listar servicios disponibles, obtener servicioId y duracionAgendaMin

3. GET /api/clientes?sucursalId={id}&telefono=1145678901
   → buscar cliente existente
   → Si no existe: POST /api/clientes  → obtener clienteId

4. POST /api/turnos
   → enviar sucursalId, clienteId, servicioId, profesionalId, fechaHoraInicio, origen
   → La API valida conflicto y devuelve el turno con tokenGestion

5. POST /api/caja/{cajaId}/movimientos   (al momento del cobro)
   → tipo: "CobroTurno", turnoId: el id del turno creado
```

### Reprogramar un turno

```
PUT /api/turnos/{id}
{
  "estado": "Reprogramado",
  "fechaHoraInicio": "2025-06-20T14:00:00"
}
```

### Consulta del cliente por token (cara pública)

```
GET /api/turnos/by-token/{tokenGestion}
```
