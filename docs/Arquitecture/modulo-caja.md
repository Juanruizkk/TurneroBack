# Módulo de Caja

Gestión de movimientos de dinero, autorización contextual por rol y reportes de liquidación por profesional.

---

## Índice

1. [Resumen](#1-resumen)
2. [Creación automática de caja](#2-creación-automática-de-caja)
3. [Modelo de dominio](#3-modelo-de-dominio)
4. [Enums de referencia](#4-enums-de-referencia)
5. [Autorización contextual](#5-autorización-contextual)
6. [Transacción atómica](#6-transacción-atómica)
7. [Endpoints](#7-endpoints)
8. [DTOs](#8-dtos)
9. [Reglas de negocio](#9-reglas-de-negocio)
10. [Resumen de liquidaciones — lógica de cálculo](#10-resumen-de-liquidaciones--lógica-de-cálculo)
11. [Archivos involucrados](#11-archivos-involucrados)

---

## 1. Resumen

Cada sucursal tiene exactamente **una caja** (`Caja`). Los movimientos de dinero se registran como `MovimientoCaja`. Cuando el movimiento corresponde a un cobro de turno, se crea además un `PagoTurno` que vincula el pago con el turno específico y registra su concepto (seña, pago parcial, pago total, saldo final).

| Recurso          | Propósito                                                     |
|------------------|---------------------------------------------------------------|
| `Caja`           | Caja registradora de la sucursal                              |
| `MovimientoCaja` | Ingreso o egreso de dinero (cobro, gasto, retiro, ajuste)     |
| `PagoTurno`      | Vínculo entre un `MovimientoCaja` de tipo `CobroTurno` y el `Turno` |

---

## 2. Creación automática de caja

Toda sucursal nace con su caja creada automáticamente. El proceso ocurre en **dos paths de creación**, ambos dentro del mismo `SaveChanges` (transacción implícita de EF Core):

| Path                         | Servicio              | Cuándo ocurre                                 |
|------------------------------|-----------------------|-----------------------------------------------|
| Sucursal individual          | `SucursalService`     | `POST /api/sucursales`                        |
| Sucursal + owner (SuperAdmin)| `SuperAdminService`   | `POST /api/superadmin/sucursales`             |

### Código en `SucursalService.CreateAsync`

```csharp
await _sucursalRepository.AddAsync(sucursal);

// Toda sucursal nace con su caja
await _cajaRepository.AddAsync(new Caja
{
    Id         = Guid.NewGuid(),
    SucursalId = sucursal.Id,
    Nombre     = $"Caja — {dto.Nombre}",
    Activa     = true,
});

await _sucursalRepository.SaveChangesAsync();
```

El mismo bloque existe en `SuperAdminService.CrearConOwnerAsync`, donde la caja es el paso 3 de 4 (Sucursal → Owner → Caja → SaveChanges).

### Sucursales preexistentes sin caja

Para sucursales creadas antes de esta feature, ejecutar el siguiente SQL una sola vez.
Usa `WHERE NOT EXISTS` para que sea idempotente (no crea duplicados si ya tiene caja):

```sql
INSERT INTO cajas ("Id", "SucursalId", "Nombre", "Activa")
SELECT gen_random_uuid(), s."Id", 'Caja — ' || s."Nombre", true
FROM sucursales s
WHERE NOT EXISTS (
    SELECT 1 FROM cajas c WHERE c."SucursalId" = s."Id"
);
```

```bash
# Ejecutar desde la máquina host
docker exec turnero_db psql -U postgres -d turnero -c "<SQL de arriba>"
```

---

## 3. Modelo de dominio

### `Caja`

```csharp
public class Caja
{
    public Guid   Id         { get; set; }
    public Guid   SucursalId { get; set; }
    public string Nombre     { get; set; }
    public bool   Activa     { get; set; }

    public Sucursal              Sucursal    { get; set; }
    public ICollection<MovimientoCaja> Movimientos { get; set; }
}
```

Relación: `Sucursal` 1 ↔ 1 `Caja` (índice único en `SucursalId`).

---

### `MovimientoCaja`

```csharp
public class MovimientoCaja
{
    public Guid                Id             { get; set; }
    public Guid                CajaId         { get; set; }
    public Guid?               UsuarioId      { get; set; }   // null = movimiento automático del sistema
    public Guid?               TurnoId        { get; set; }   // null para gastos/retiros/ajustes
    public TipoMovimientoCaja  TipoMovimiento { get; set; }
    public MedioPago           MedioPago      { get; set; }
    public decimal             Monto          { get; set; }   // numeric(18,2)
    public string?             Observaciones  { get; set; }
    public DateTime            Fecha          { get; set; }   // UTC

    public Caja       Caja      { get; set; }
    public Usuario?   Usuario   { get; set; }   // null si UsuarioId es null
    public Turno?     Turno     { get; set; }
    public PagoTurno? PagoTurno { get; set; }
}
```

> **Nota:** `UsuarioId` es `Guid?` desde la integración con MercadoPago. Los movimientos registrados automáticamente por el webhook de pago (cobro de seña online) tienen `UsuarioId = null` para indicar que el actor es el sistema, no un usuario humano. Los clientes del frontend que muestren el nombre del cajero deben manejar `usuarioId: null` en la respuesta.

---

### `PagoTurno`

```csharp
public class PagoTurno
{
    public Guid              Id               { get; set; }
    public Guid              TurnoId          { get; set; }
    public Guid              MovimientoCajaId { get; set; }
    public ConceptoPagoTurno Concepto         { get; set; }
    public decimal           Monto            { get; set; }   // numeric(18,2)
    public DateTime          Fecha            { get; set; }

    public Turno          Turno          { get; set; }
    public MovimientoCaja MovimientoCaja { get; set; }
}
```

Relación `MovimientoCaja` 1 ↔ 0..1 `PagoTurno` (un cobro de turno puede o no tener concepto de pago explícito).

---

## 4. Enums de referencia

### `TipoMovimientoCaja`

| Valor           | Impacto en saldo | Descripción                                      |
|-----------------|------------------|--------------------------------------------------|
| `CobroTurno`    | Ingreso (+)      | Cobro de un turno                                |
| `IngresoManual` | Ingreso (+)      | Ingreso de efectivo sin turno asociado           |
| `Ajuste`        | + o −            | Ajuste contable (signo determinado por el monto) |
| `Gasto`         | Egreso (−)       | Gasto operativo                                  |
| `Retiro`        | Egreso (−)       | Retiro de efectivo por el dueño o liquidación    |

> **Convención de signo:** los montos se almacenan siempre como valores positivos.
> El `TipoMovimiento` determina si impactan positiva o negativamente en el saldo.
> La única excepción es `Ajuste`, donde el signo del monto almacenado indica la dirección.

### `MedioPago`

`Efectivo` · `MercadoPago` · `Transferencia` · `Tarjeta` · `Otro`

### `ConceptoPagoTurno`

`Sena` · `PagoParcial` · `PagoTotal` · `SaldoFinal`

---

## 5. Autorización contextual

El endpoint `POST /movimientos` extrae el `UsuarioId` y el `Rol` **directamente del JWT** — nunca del body de la request.

```
Token JWT → Claim "userId"        → Guid del usuario que opera
          → Claim ClaimTypes.Role → "Administrativo" | "Caja" | "Profesional"
```

### Regla para `CobroTurno`

| Rol del usuario   | Puede cobrar                          |
|-------------------|---------------------------------------|
| `Administrativo`  | Cualquier turno                       |
| `Caja`            | Cualquier turno                       |
| `Profesional`     | **Solo sus propios turnos** (`Turno.ProfesionalId == usuarioId`) |

Si un `Profesional` intenta cobrar un turno de otro profesional, el servicio lanza `UnauthorizedAccessException` y el controller devuelve `403 Forbidden`.

```csharp
// CajaService.cs — validación contextual
if (string.Equals(rolUsuario, RolUsuario.Profesional.ToString(), StringComparison.OrdinalIgnoreCase)
    && turno.ProfesionalId != usuarioId)
{
    throw new UnauthorizedAccessException(
        "Un profesional solo puede registrar cobros de sus propios turnos.");
}
```

### Reporte de liquidaciones

El endpoint `GET /resumen-liquidaciones` está restringido a `[Authorize(Roles = "Administrativo")]`.
Solo el owner o usuarios con rol Administrativo pueden acceder a datos financieros del período.

---

## 6. Transacción atómica

Cuando se registra un `CobroTurno`, tres operaciones deben completarse o fallar juntas:

```
BeginTransaction
│
├── INSERT movimientos_caja
│
├── INSERT pagos_turno          ← solo si ConceptoPago fue especificado
│
└── UPDATE turnos
    SET TotalCobrado = TotalCobrado + :monto
    WHERE Id = :turnoId

Commit / Rollback automático en caso de error
```

La transacción vive en el repositorio de infraestructura (`CajaRepository`), que es el único nivel con acceso al `AppDbContext`:

```csharp
// CajaRepository.cs
public async Task<MovimientoCaja> RegistrarMovimientoTransaccionalAsync(
    MovimientoCaja movimiento, PagoTurno? pagoTurno, Turno? turnoAActualizar)
{
    await using var tx = await _context.Database.BeginTransactionAsync();
    try
    {
        await _context.MovimientosCaja.AddAsync(movimiento);
        if (pagoTurno is not null)       await _context.PagosTurno.AddAsync(pagoTurno);
        if (turnoAActualizar is not null) _context.Turnos.Update(turnoAActualizar);

        await _context.SaveChangesAsync();
        await tx.CommitAsync();
        return movimiento;
    }
    catch { await tx.RollbackAsync(); throw; }
}
```

Para movimientos que **no** son `CobroTurno` (gastos, retiros, ajustes, ingresos manuales), `pagoTurno` y `turnoAActualizar` son `null` y la transacción solo inserta el `MovimientoCaja`.

---

## 7. Endpoints

### `GET /api/caja/sucursal/{sucursalId}`
Devuelve la caja de una sucursal.

| Código | Condición                      |
|--------|--------------------------------|
| `200`  | OK                             |
| `404`  | No existe caja para la sucursal|

---

### `GET /api/caja/{cajaId}/movimientos`
Lista los movimientos de una caja con filtros opcionales de fecha.

| Query param | Tipo       | Descripción              |
|-------------|------------|--------------------------|
| `desde`     | `DateTime` | Fecha mínima (opcional)  |
| `hasta`     | `DateTime` | Fecha máxima (opcional)  |

Resultado ordenado por `Fecha` descendente.

---

### `POST /api/caja/{cajaId}/movimientos`
Registra un nuevo movimiento. **Requiere JWT.**

**Body:** `RegistrarMovimientoDto` (sin `UsuarioId` — se extrae del token)

```json
{
  "turnoId":       "3fa85f64-...",     // obligatorio solo para CobroTurno
  "tipoMovimiento": "CobroTurno",
  "medioPago":      "Efectivo",
  "monto":          15000.00,
  "conceptoPago":   "PagoTotal",       // opcional, aplica solo a CobroTurno
  "observaciones":  "Pago completo"    // opcional
}
```

| Código | Condición                                              |
|--------|--------------------------------------------------------|
| `201`  | Movimiento registrado                                  |
| `400`  | Falta TurnoId en CobroTurno, monto ≤ 0, etc.          |
| `403`  | Profesional intentando cobrar turno de otro            |
| `404`  | Caja o Turno no encontrado                             |

---

### `GET /api/caja/{cajaId}/resumen-liquidaciones`
Resumen financiero del período y liquidaciones por profesional.
**Solo para rol `Administrativo`.**

| Query param | Tipo       | Requerido | Descripción                                                  |
|-------------|------------|-----------|--------------------------------------------------------------|
| `desde`     | `DateTime` | Sí        | Inicio del período                                           |
| `hasta`     | `DateTime` | No        | Fin del período. Si se omite, el período es solo el día `desde` |

**Ejemplo — cierre diario:**
```
GET /api/caja/{cajaId}/resumen-liquidaciones?desde=2026-03-16
```

**Ejemplo — rango mensual:**
```
GET /api/caja/{cajaId}/resumen-liquidaciones?desde=2026-03-01&hasta=2026-03-31
```

| Código | Condición                          |
|--------|------------------------------------|
| `200`  | Resumen generado                   |
| `400`  | `desde` faltante o `hasta` < `desde` |
| `403`  | Rol sin permiso                    |
| `404`  | Caja no encontrada                 |

---

## 8. DTOs

### `RegistrarMovimientoDto` (request)

| Campo           | Tipo                    | Requerido | Descripción                                     |
|-----------------|-------------------------|-----------|-------------------------------------------------|
| `turnoId`       | `Guid?`                 | Condicional | Obligatorio si `TipoMovimiento == CobroTurno` |
| `tipoMovimiento`| `TipoMovimientoCaja`    | Sí        |                                                 |
| `medioPago`     | `MedioPago`             | Sí        |                                                 |
| `monto`         | `decimal`               | Sí        | Mayor a 0                                       |
| `conceptoPago`  | `ConceptoPagoTurno?`    | No        | Si presente, genera `PagoTurno`                 |
| `observaciones` | `string?`               | No        | Máx. 1000 chars                                 |

---

### `MovimientoCajaResponseDto` (response)

```json
{
  "id":             "guid",
  "cajaId":         "guid",
  "usuarioId":      "guid",
  "turnoId":        "guid | null",
  "tipoMovimiento": "CobroTurno",
  "medioPago":      "Efectivo",
  "monto":          15000.00,
  "observaciones":  "string | null",
  "fecha":          "2026-03-16T14:30:00Z"
}
```

---

### `ResumenLiquidacionDto` (response)

```json
{
  "fechaDesde":    "2026-03-16T00:00:00Z",
  "fechaHasta":    "2026-03-16T23:59:59.9999999Z",
  "totalIngresos": 85000.00,
  "totalEgresos":  12000.00,
  "saldoPeriodo":  73000.00,
  "liquidacionesPorProfesional": [
    {
      "profesionalId":          "guid",
      "nombre":                 "Ana Rodríguez",
      "cantidadTurnosAtendidos": 6,
      "totalGenerado":           45000.00
    },
    {
      "profesionalId":          "guid",
      "nombre":                 "Laura Gómez",
      "cantidadTurnosAtendidos": 4,
      "totalGenerado":           32000.00
    }
  ]
}
```

La lista `liquidacionesPorProfesional` está ordenada por `TotalGenerado` descendente.

---

## 9. Reglas de negocio

### Validaciones del servicio

| Regla                                                        | Error devuelto   |
|--------------------------------------------------------------|------------------|
| `CobroTurno` sin `TurnoId`                                   | `400 Bad Request`|
| `TurnoId` no existe en la base de datos                      | `404 Not Found`  |
| `Caja` no existe                                             | `404 Not Found`  |
| `Profesional` intentando cobrar turno de otro profesional    | `403 Forbidden`  |
| `monto` ≤ 0                                                  | `400 Bad Request`|

### Actualización de `Turno.TotalCobrado`

Cada `CobroTurno` acumula sobre `Turno.TotalCobrado`:

```
TotalCobrado += dto.Monto
```

Este campo permite calcular en cualquier momento el saldo pendiente del turno:

```
SaldoPendiente = Turno.PrecioServicio - Turno.TotalCobrado
```

### Fechas en UTC

Todos los `MovimientoCaja.Fecha` se almacenan en **UTC** (`DateTime.UtcNow`). Los filtros del resumen convierten las fechas recibidas a UTC antes de la consulta:

```csharp
var desdeUtc = desde.Date.ToUniversalTime();
var hastaUtc = (hastaParam ?? desde).Date.AddDays(1).AddTicks(-1).ToUniversalTime();
```

---

## 10. Resumen de liquidaciones — lógica de cálculo

### Clasificación de movimientos

```
TotalIngresos = Σ monto  donde TipoMovimiento ∈ { CobroTurno, IngresoManual }
              + Σ monto  donde TipoMovimiento = Ajuste AND monto > 0

TotalEgresos  = Σ |monto| donde TipoMovimiento ∈ { Gasto, Retiro }
              + Σ |monto| donde TipoMovimiento = Ajuste AND monto < 0

SaldoPeriodo  = TotalIngresos − TotalEgresos
```

### Liquidaciones por profesional

Se filtran únicamente los movimientos de tipo `CobroTurno` con `TurnoId` no nulo. Se hace un JOIN con `Turno → Profesional` y se agrupa por `ProfesionalId`:

```
Para cada grupo (profesionalId):
  CantidadTurnosAtendidos = COUNT(DISTINCT TurnoId)
  TotalGenerado           = SUM(monto)
  Nombre                  = Profesional.Nombre + " " + Profesional.Apellido
```

> **Nota:** `CantidadTurnosAtendidos` usa `DISTINCT TurnoId` porque un turno puede tener
> más de un cobro (seña + saldo final). Se cuenta el turno una sola vez.

---

## 11. Archivos involucrados

| Capa           | Archivo                                                              | Cambio                                                        |
|----------------|----------------------------------------------------------------------|---------------------------------------------------------------|
| Domain         | `Domain/Entities/Caja.cs`                                            | Entidad existente (sin cambios)                               |
| Domain         | `Domain/Entities/MovimientoCaja.cs`                                  | Entidad existente (sin cambios)                               |
| Domain         | `Domain/Entities/PagoTurno.cs`                                       | Entidad existente (sin cambios)                               |
| Domain         | `Domain/Enums/TipoMovimientoCaja.cs`                                 | Enum existente (sin cambios)                                  |
| Domain         | `Domain/Enums/ConceptoPagoTurno.cs`                                  | Enum existente (sin cambios)                                  |
| Domain         | `Domain/Enums/MedioPago.cs`                                          | Enum existente (sin cambios)                                  |
| Application    | `Application/DTOs/Caja/RegistrarMovimientoDto.cs`                    | Nuevo — entrada del POST (sin UsuarioId)                      |
| Application    | `Application/DTOs/Caja/ResumenLiquidacionDto.cs`                     | Nuevo — respuesta del reporte                                 |
| Application    | `Application/DTOs/Caja/LiquidacionProfesionalDto.cs`                 | Nuevo — por profesional en el reporte                         |
| Application    | `Application/Interfaces/ICajaRepository.cs`                          | +2 métodos: transaccional + queries para reporte              |
| Application    | `Application/Services/CajaService.cs`                                | Reescrito: auth contextual + transacción + reporte            |
| Application    | `Application/Services/SucursalService.cs`                            | Inyecta `ICajaRepository`; crea `Caja` al crear `Sucursal`   |
| Application    | `Application/Services/SuperAdminService.cs`                          | Inyecta `ICajaRepository`; crea `Caja` al crear con owner    |
| Infrastructure | `Infrastructure/Repositories/CajaRepository.cs`                      | Implementa los nuevos métodos con `BeginTransactionAsync`     |
| Api            | `Api/Controllers/CajaController.cs`                                  | `[Authorize]`, extracción JWT, endpoint de liquidaciones      |

---

---

## Cambios relacionados con MercadoPago

Para documentación detallada del flujo de pagos online, ver [`mercadopago-senas.md`](mercadopago-senas.md).

Resumen de cambios en este módulo:

| Campo | Antes | Después | Motivo |
|---|---|---|---|
| `MovimientoCaja.UsuarioId` | `Guid` (no nullable) | `Guid?` (nullable) | Movimientos automáticos vía webhook no tienen usuario humano |
| `MovimientoCajaResponseDto.UsuarioId` | `Guid` | `Guid?` | Refleja el dominio |

---

*Documentación actualizada el 2026-03-18.*
