# Módulo de Caja

Gestión de movimientos de dinero, sesiones de caja, autorización contextual por rol y reportes de liquidación por profesional.

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
11. [Sesiones de Caja](#11-sesiones-de-caja)
12. [Frontend — Caja.tsx](#12-frontend--cajatsx)
13. [Archivos involucrados](#13-archivos-involucrados)

---

## 1. Resumen

Cada sucursal tiene exactamente **una caja** (`Caja`). Los movimientos de dinero se registran como `MovimientoCaja`. Cuando el movimiento corresponde a un cobro de turno, se crea además un `PagoTurno` que vincula el pago con el turno específico y registra su concepto (seña, pago parcial, pago total, saldo final).

La caja opera a través de **sesiones** (`CajaSesion`). Una sesión encapsula un período operativo con apertura (fondo inicial declarado) y cierre (monto real contado, diferencia calculada). Cada movimiento registrado durante una sesión activa queda vinculado automáticamente a ella.

| Recurso          | Propósito                                                                    |
|------------------|------------------------------------------------------------------------------|
| `Caja`           | Caja registradora de la sucursal                                             |
| `CajaSesion`     | Período operativo con apertura y cierre (turno de trabajo del cajero)        |
| `MovimientoCaja` | Ingreso o egreso de dinero (cobro, gasto, retiro, ajuste)                    |
| `PagoTurno`      | Vínculo entre un `MovimientoCaja` de tipo `CobroTurno` y el `Turno`          |

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
    public Guid?               UsuarioId      { get; set; }    // null = movimiento automático del sistema
    public Guid?               TurnoId        { get; set; }    // null para gastos/retiros/ajustes
    public Guid?               CajaSesionId   { get; set; }    // null = movimiento sin sesión activa
    public TipoMovimientoCaja  TipoMovimiento { get; set; }
    public MedioPago           MedioPago      { get; set; }
    public decimal             Monto          { get; set; }    // numeric(18,2)
    public string?             Observaciones  { get; set; }
    public DateTime            Fecha          { get; set; }    // timestamptz (UTC)

    public Caja        Caja       { get; set; }
    public Usuario?    Usuario    { get; set; }
    public Turno?      Turno      { get; set; }
    public PagoTurno?  PagoTurno  { get; set; }
    public CajaSesion? CajaSesion { get; set; }
}
```

> **Nota:** `UsuarioId` es `Guid?` desde la integración con MercadoPago. Los movimientos registrados automáticamente por el webhook de pago (cobro de seña online) tienen `UsuarioId = null` para indicar que el actor es el sistema, no un usuario humano. Los clientes del frontend que muestren el nombre del cajero deben manejar `usuarioId: null` en la respuesta.

> **Nota:** `CajaSesionId` es `Guid?` para compatibilidad con movimientos históricos registrados antes de la existencia de sesiones. Los movimientos anteriores al deploy de esta feature tienen `CajaSesionId = null`.

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

### `EstadoCajaSesion`

| Valor     | Int | Descripción                          |
|-----------|-----|--------------------------------------|
| `Abierta` | `0` | Sesión en curso, acepta movimientos  |
| `Cerrada` | `1` | Sesión finalizada, datos inmutables  |

Persistido como `int` en la DB (`HasConversion<int>()`).

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
        // Auto-vincular a la sesión activa si existe
        var sesionActiva = await _context.CajaSesiones
            .FirstOrDefaultAsync(s => s.CajaId == movimiento.CajaId
                                   && s.Estado == EstadoCajaSesion.Abierta);
        if (sesionActiva is not null)
            movimiento.CajaSesionId = sesionActiva.Id;

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

El auto-link a la sesión aplica a **todos** los tipos de movimiento, incluidos los registrados por el webhook de MercadoPago. Si no hay sesión activa en el momento del registro, `CajaSesionId` queda `null` — el movimiento se guarda igualmente.

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

### `GET /api/caja/{cajaId}/sesiones/activa`
Devuelve la sesión actualmente abierta, o `204 No Content` si no hay ninguna.

| Código | Condición               |
|--------|-------------------------|
| `200`  | Sesión activa encontrada |
| `204`  | No hay sesión abierta   |

---

### `GET /api/caja/{cajaId}/sesiones`
Historial completo de sesiones de la caja, ordenadas por `FechaApertura` descendente.

| Código | Condición |
|--------|-----------|
| `200`  | Lista (puede ser vacía) |

---

### `POST /api/caja/{cajaId}/sesiones/abrir`
Abre una nueva sesión de caja. **Requiere sesión cerrada.**

**Body:** `AbrirSesionDto`

```json
{
  "montoInicial":  5000.00,
  "observaciones": "Turno mañana"   // opcional
}
```

| Código | Condición                           |
|--------|-------------------------------------|
| `201`  | Sesión abierta                      |
| `400`  | Ya existe una sesión activa         |

---

### `POST /api/caja/{cajaId}/sesiones/cerrar`
Cierra la sesión activa. El servidor calcula `MontoFinalEsperado` y `Diferencia`.

**Body:** `CerrarSesionDto`

```json
{
  "montoFinalReal": 4800.00,
  "observaciones":  "Falta revisar cobro de las 14hs"   // opcional
}
```

| Código | Condición                           |
|--------|-------------------------------------|
| `200`  | Sesión cerrada con ticket de corte  |
| `400`  | No hay sesión activa                |

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

## 11. Sesiones de Caja

### Concepto

Una **sesión de caja** (`CajaSesion`) representa un período operativo con inicio y fin explícitos. Es el equivalente a "abrir la caja" al empezar el turno y "cerrar la caja" al terminar.

```
Apertura                                          Cierre
   │                                                │
   │  MontoInicial = $5.000 (fondo en efectivo)     │  MontoFinalReal = $4.800 (lo que el cajero contó)
   │                                                │
   │←───────── todos los movimientos del turno ────→│
   │           quedan vinculados a esta sesión       │
                                                    │
                                            MontoFinalEsperado = $5.000 + ingresos − egresos
                                            Diferencia = $4.800 − MontoFinalEsperado
```

### Entidad `CajaSesion`

```csharp
public class CajaSesion
{
    public Guid      Id                      { get; set; }
    public Guid      CajaId                  { get; set; }
    public DateTime  FechaApertura           { get; set; }    // timestamp without time zone
    public DateTime? FechaCierre             { get; set; }    // timestamp without time zone; null si está abierta
    public decimal   MontoInicial            { get; set; }    // fondo declarado al abrir
    public decimal?  MontoFinalEsperado      { get; set; }    // calculado al cerrar
    public decimal?  MontoFinalReal          { get; set; }    // ingresado por el cajero al cerrar
    public decimal?  Diferencia              { get; set; }    // MontoFinalReal − MontoFinalEsperado
    public EstadoCajaSesion Estado           { get; set; }    // Abierta | Cerrada
    public Guid?     UsuarioAperturaId       { get; set; }
    public Guid?     UsuarioCierreId         { get; set; }
    public string?   ObservacionesApertura   { get; set; }
    public string?   ObservacionesCierre     { get; set; }

    public Caja                        Caja            { get; set; }
    public Usuario?                    UsuarioApertura { get; set; }
    public Usuario?                    UsuarioCierre   { get; set; }
    public ICollection<MovimientoCaja> Movimientos     { get; set; }
}
```

### Tabla en la DB: `caja_sesiones`

| Columna                  | Tipo                   | Nullable | Descripción                                  |
|--------------------------|------------------------|----------|----------------------------------------------|
| `Id`                     | `uuid`                 | No       | PK                                           |
| `CajaId`                 | `uuid`                 | No       | FK → `cajas.Id` (Restrict)                   |
| `FechaApertura`          | `timestamp without tz` | No       |                                              |
| `FechaCierre`            | `timestamp without tz` | Sí       | Nulo mientras la sesión está abierta         |
| `MontoInicial`           | `numeric(18,2)`        | No       |                                              |
| `MontoFinalEsperado`     | `numeric(18,2)`        | Sí       | Calculado y persistido al cerrar             |
| `MontoFinalReal`         | `numeric(18,2)`        | Sí       | Ingresado por el cajero al cerrar            |
| `Diferencia`             | `numeric(18,2)`        | Sí       | `MontoFinalReal − MontoFinalEsperado`        |
| `Estado`                 | `int`                  | No       | `0=Abierta, 1=Cerrada`                       |
| `UsuarioAperturaId`      | `uuid`                 | Sí       | FK → `usuarios.Id` (SetNull)                 |
| `UsuarioCierreId`        | `uuid`                 | Sí       | FK → `usuarios.Id` (SetNull)                 |
| `ObservacionesApertura`  | `varchar(500)`         | Sí       |                                              |
| `ObservacionesCierre`    | `varchar(500)`         | Sí       |                                              |

**Índice de performance:** `(CajaId, Estado)` — optimiza la query "sesión activa" que se ejecuta en cada registro de movimiento.

**Columna `CajaSesionId` en `movimientos_caja`:** `uuid` nullable. FK con `OnDelete(SetNull)`. Los registros históricos anteriores al deploy tienen `NULL`.

### Fórmula de cierre

```
MontoFinalEsperado = MontoInicial
                   + Σ(monto)    donde tipo ∈ { CobroTurno, IngresoManual } o (Ajuste con monto > 0)
                   − Σ(|monto|)  donde tipo ∈ { Gasto, Retiro } o (Ajuste con monto < 0)

Diferencia = MontoFinalReal − MontoFinalEsperado

Diferencia > 0 → Sobrante
Diferencia < 0 → Faltante
Diferencia = 0 → Cuadra perfecto
```

Los movimientos usados para el cálculo son solo los vinculados a esa sesión (`CajaSesionId = sesion.Id`).

### Invariantes del sistema

| Regla | Implementación |
|---|---|
| Solo una sesión `Abierta` por caja | El servicio llama `GetSesionActivaByCajaAsync` antes de abrir y lanza `InvalidOperationException` si ya existe |
| No se puede abrir si ya hay una activa | `400 Bad Request` |
| No se puede cerrar si no hay sesión activa | `400 Bad Request` |
| Los movimientos históricos sin sesión tienen `CajaSesionId = null` | Columna nullable; compatible con datos preexistentes |
| `MontoFinalEsperado` y `Diferencia` son inmutables tras el cierre | No existe endpoint de actualización; se calculan y persisten una sola vez |

### `DateTime` y zonas horarias en sesiones

`FechaApertura` y `FechaCierre` se almacenan como `timestamp without time zone`. Npgsql requiere `DateTimeKind.Unspecified` para esta columna:

```csharp
// Correcto para timestamp without time zone
FechaApertura = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
```

Al usar `FechaApertura` para filtrar movimientos (columna `timestamptz`), se convierte explícitamente:

```csharp
// Al comparar contra movimientos_caja.Fecha (timestamptz), Npgsql necesita Kind=Utc
var desdeUtc = DateTime.SpecifyKind(sesion.FechaApertura, DateTimeKind.Utc);
```

### DTOs de sesión

#### `AbrirSesionDto` (request)

| Campo           | Tipo      | Requerido | Descripción                        |
|-----------------|-----------|-----------|------------------------------------|
| `montoInicial`  | `decimal` | Sí        | Fondo en caja al abrir. `≥ 0`      |
| `observaciones` | `string?` | No        | Máx. 500 chars                     |

#### `CerrarSesionDto` (request)

| Campo            | Tipo      | Requerido | Descripción                                    |
|------------------|-----------|-----------|------------------------------------------------|
| `montoFinalReal` | `decimal` | Sí        | Lo que el cajero contó físicamente. `≥ 0`      |
| `observaciones`  | `string?` | No        | Máx. 500 chars                                 |

#### `CajaSesionResponseDto` (response)

```json
{
  "id":                    "guid",
  "cajaId":                "guid",
  "fechaApertura":         "2026-03-18T09:00:00",
  "fechaCierre":           "2026-03-18T18:30:00",
  "montoInicial":          5000.00,
  "montoFinalEsperado":    62000.00,
  "montoFinalReal":        61500.00,
  "diferencia":            -500.00,
  "estado":                "Cerrada",
  "usuarioAperturaNombre": "Juan Ruiz",
  "usuarioCierreNombre":   "Juan Ruiz",
  "observacionesApertura": "Turno mañana",
  "observacionesCierre":   "Faltaría revisar cobro de las 14hs"
}
```

---

## 12. Frontend — Caja.tsx

### Estado del componente

```typescript
// Sesiones
const [sesionActiva, setSesionActiva] = useState<CajaSesion | null>(null);
const [sesiones,     setSesiones]     = useState<CajaSesion[]>([]);
const [tab,          setTab]          = useState<'movimientos' | 'historial'>('movimientos');
const [modalSesion,  setModalSesion]  = useState<'abrir' | 'cerrar' | null>(null);

// Formularios de sesión
const [montoInicial,   setMontoInicial]   = useState<number>(0);
const [obsApertura,    setObsApertura]    = useState('');
const [montoFinalReal, setMontoFinalReal] = useState<number>(0);
const [obsCierre,      setObsCierre]      = useState('');
```

### Flujo de apertura

```
Usuario click "Abrir sesión"
  → Modal con "Fondo inicial" (≥ 0, obligatorio) + observaciones (opcional)
  → POST /api/caja/{id}/sesiones/abrir
  → setSesionActiva(sesion)
  → Banner cambia a verde con fondo inicial y nombre del responsable
  → Botón "Registrar movimiento" se habilita
```

### Flujo de cierre — Ticket de Corte

```
Usuario click "Cerrar caja"
  → Modal "Corte de Caja" (estilo ticket con líneas punteadas):
      ┌─────────────────────────────────────┐
      │ Corte de Caja                       │
      │ Apertura: 18/03/2026 09:00 · Juan   │
      ├ ─ ─ Resumen del sistema ─ ─ ─ ─ ─ ─┤
      │ Fondo inicial          $5.000       │
      │ + Ingresos de sesión   $57.000      │
      │ − Egresos de sesión    $0           │
      │ Debería haber          $62.000      │
      ├ ─ ─ Conteo real ─ ─ ─ ─ ─ ─ ─ ─ ─ ┤
      │ ¿Cuánto contaste? [   61500   ]     │
      ├ ─ ─ Resultado ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┤
      │ Diferencia             −$500        │
      │ "Faltante — revisar movimientos"    │
      ├ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─┤
      │ Observaciones: [ ...            ]   │
      │ [Cancelar]       [Confirmar cierre] │
      └─────────────────────────────────────┘
  → POST /api/caja/{id}/sesiones/cerrar
  → setSesionActiva(null)
  → Banner cambia a amarillo
  → Botón "Registrar movimiento" se deshabilita
```

### Diferencia reactiva en el modal

```typescript
// Aproximación visual en tiempo real (período actualmente filtrado)
const montoEsperadoCierre = sesionActiva
  ? sesionActiva.montoInicial + ingresosActuales - egresosActuales
  : 0;
const diferenciaCierre = montoFinalReal - montoEsperadoCierre;
```

> El servidor recalcula el valor definitivo usando solo los movimientos con `CajaSesionId = sesion.Id`. La cifra del modal es orientativa; el ticket guardado es el oficial.

### Indicadores visuales

| Estado         | Banner                        | Pill bajo cards                         | Botón "Registrar" |
|----------------|-------------------------------|-----------------------------------------|-------------------|
| Sesión abierta | Verde + info + "Cerrar caja"  | Verde · "Abierta · fondo $5.000"        | Habilitado        |
| Sin sesión     | Amarillo + "Abrir sesión"     | Naranja · "Sesión actual: Cerrada"      | Deshabilitado + tooltip |

### Regla de bloqueo de movimientos

```typescript
<button
  disabled={!caja || !sesionActiva}
  title={!sesionActiva ? 'Abrí una sesión de caja para registrar movimientos' : undefined}
>
  Registrar movimiento
</button>
```

### Tab "Historial de sesiones"

Tabla con: fecha apertura, fecha cierre, responsable, fondo inicial, monto esperado, monto real, diferencia (roja si negativa, verde si positiva), estado (badge).

Los movimientos nunca se borran ni desaparecen. El filtro de período de la vista "Movimientos" sigue funcionando independientemente de las sesiones.

---

## 13. Archivos involucrados

### Capa de dominio

| Archivo | Cambio |
|---|---|
| `Domain/Entities/Caja.cs` | `+ICollection<CajaSesion> Sesiones` |
| `Domain/Entities/MovimientoCaja.cs` | `+CajaSesionId: Guid?` + nav `CajaSesion?` |
| `Domain/Entities/CajaSesion.cs` | **Nuevo** — entidad completa |
| `Domain/Entities/PagoTurno.cs` | Sin cambios |
| `Domain/Enums/EstadoCajaSesion.cs` | **Nuevo** — `Abierta = 0, Cerrada = 1` |
| `Domain/Enums/TipoMovimientoCaja.cs` | Sin cambios |
| `Domain/Enums/ConceptoPagoTurno.cs` | Sin cambios |
| `Domain/Enums/MedioPago.cs` | Sin cambios |

### Capa de infraestructura

| Archivo | Cambio |
|---|---|
| `Infrastructure/Data/AppDbContext.cs` | `+DbSet<CajaSesion> CajaSesiones` |
| `Infrastructure/Data/Configurations/CajaSesionConfiguration.cs` | **Nuevo** — Fluent API, tabla `caja_sesiones`, índice `(CajaId, Estado)` |
| `Infrastructure/Data/Configurations/MovimientoCajaConfiguration.cs` | `+FK nullable CajaSesionId → caja_sesiones (SetNull)` |
| `Infrastructure/Repositories/CajaRepository.cs` | `+4 métodos sesión` + auto-link en `RegistrarMovimientoTransaccionalAsync` |
| `Infrastructure/Migrations/..._AddCajaSesiones.cs` | **Nuevo** — crea `caja_sesiones`, agrega `CajaSesionId` a `movimientos_caja` |

### Capa de aplicación

| Archivo | Cambio |
|---|---|
| `Application/DTOs/Caja/AbrirSesionDto.cs` | **Nuevo** — request para abrir sesión |
| `Application/DTOs/Caja/CerrarSesionDto.cs` | **Nuevo** — request para cerrar sesión |
| `Application/DTOs/Caja/CajaSesionResponseDto.cs` | **Nuevo** — response de sesión |
| `Application/DTOs/Caja/RegistrarMovimientoDto.cs` | Sin cambios |
| `Application/DTOs/Caja/ResumenLiquidacionDto.cs` | Sin cambios |
| `Application/DTOs/Caja/LiquidacionProfesionalDto.cs` | Sin cambios |
| `Application/Interfaces/ICajaRepository.cs` | `+4 métodos: GetSesionActiva, AbrirSesion, CerrarSesion, GetSesiones` |
| `Application/Services/CajaService.cs` | `+4 métodos de sesión` + `MapSesionToResponse` |
| `Application/Services/SucursalService.cs` | Sin cambios (crea `Caja` al crear `Sucursal`) |
| `Application/Services/SuperAdminService.cs` | Sin cambios |

### Capa de API

| Archivo | Cambio |
|---|---|
| `Api/Controllers/CajaController.cs` | `+4 endpoints: GET activa, GET sesiones, POST abrir, POST cerrar` |

### Frontend

| Archivo | Cambio |
|---|---|
| `frontend/src/types/index.ts` | `+CajaSesion, AbrirSesionDto, CerrarSesionDto` |
| `frontend/src/api/caja.ts` | `+obtenerSesionActiva, abrirSesion, cerrarSesion, listarSesiones` |
| `frontend/src/pages/admin/Caja.tsx` | Banner sesión, bloqueo de registrar, pill indicador, modal ticket, tab historial |

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

*Documentación actualizada el 2026-03-18. Incluye: movimientos, PagoTurno, autorización contextual, liquidaciones, sesiones de caja (CajaSesion), ticket de corte y frontend.*
