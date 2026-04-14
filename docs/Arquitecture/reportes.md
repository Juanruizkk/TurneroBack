# Módulo de Reportes

El módulo de reportes provee estadísticas del negocio filtradas por rango de fechas. Sigue el patrón repositorio directo (sin capa de servicio intermedia), ya que las queries son de solo lectura y no tienen lógica de negocio compleja.

---

## Archivos

| Capa | Archivo |
|---|---|
| Controller | `src/Api/Controllers/ReportsController.cs` |
| Interface | `src/Application/Interfaces/IReportRepository.cs` |
| Repository | `src/Infrastructure/Repositories/ReportRepository.cs` |
| DTOs | `src/Application/DTOs/Reportes/ReporteTurnosDto.cs` |
| Registro DI | `src/Api/Program.cs` (línea ~76) |

---

## Flujo de datos

```
GET /api/reportes/resumen?desde=&hasta=
  → ReportsController
      → extrae sucursalId del JWT
      → llama IReportRepository (5 métodos en paralelo)
          → GetTurnosAsync        → query sobre Turnos (agrupado por EstadoTurno)
          → GetIngresosAsync      → query sobre MovimientosCaja (agrupado por MedioPago y por día)
          → GetTopServiciosAsync  → query sobre Turnos completados (agrupado por Servicio)
          → GetTopProfesionalesAsync → query sobre Turnos completados (agrupado por Profesional)
          → GetClientesAsync      → query sobre Turnos (clientes únicos, nuevos vs recurrentes)
      → devuelve ReporteResumenDto
```

---

## Endpoints

Todos requieren `[Authorize]`. El `sucursalId` se extrae del claim del JWT — no va en la URL.

### `GET /api/reportes/resumen`

Reporte completo del negocio en un solo request.

**Query params:**
| Parámetro | Tipo | Descripción |
|---|---|---|
| `desde` | `DateTime` | Inicio del período (inclusive) |
| `hasta` | `DateTime` | Fin del período (inclusive, se extiende al final del día internamente) |

**Response:** `ReporteResumenDto`

```json
{
  "turnos": {
    "total": 42,
    "completados": 30,
    "cancelados": 5,
    "noShow": 2,
    "pendientes": 3,
    "pendientePago": 2
  },
  "ingresos": {
    "totalCobrado": 85000.00,
    "totalEfectivo": 50000.00,
    "totalTransferencia": 10000.00,
    "totalTarjeta": 5000.00,
    "totalMercadoPago": 20000.00,
    "porDia": [
      { "fecha": "2026-03-01", "monto": 5000.00 },
      ...
    ]
  },
  "topServicios": [
    { "servicioId": "...", "nombre": "Corte", "cantidadTurnos": 15, "ingresos": 30000.00 }
  ],
  "topProfesionales": [
    { "profesionalId": "...", "nombre": "Juan Pérez", "cantidadTurnos": 20, "ingresos": 50000.00 }
  ],
  "clientes": {
    "totalNuevos": 8,
    "totalRecurrentes": 14,
    "totalUnicos": 22
  }
}
```

### `GET /api/reportes/turnos`

Solo el bloque de estadísticas de turnos. Mismos query params que `/resumen`.

### `GET /api/reportes/ingresos`

Solo el bloque de ingresos. Mismos query params que `/resumen`.

---

## Controller

```csharp
[ApiController]
[Route("api/reportes")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IReportRepository _reportRepo;

    private Guid GetSucursalId() =>
        Guid.Parse(User.FindFirst("sucursalId")?.Value
            ?? throw new UnauthorizedAccessException("Token sin sucursalId."));

    [HttpGet("resumen")]
    public async Task<IActionResult> Resumen([FromQuery] DateTime desde, [FromQuery] DateTime hasta)
    {
        var sucursalId = GetSucursalId();
        var desdeUtc   = DateTime.SpecifyKind(desde, DateTimeKind.Unspecified);
        var hastaUtc   = DateTime.SpecifyKind(hasta.Date.AddDays(1).AddTicks(-1), DateTimeKind.Unspecified);

        return Ok(new ReporteResumenDto
        {
            Turnos           = await _reportRepo.GetTurnosAsync(sucursalId, desdeUtc, hastaUtc),
            Ingresos         = await _reportRepo.GetIngresosAsync(sucursalId, desdeUtc, hastaUtc),
            TopServicios     = await _reportRepo.GetTopServiciosAsync(sucursalId, desdeUtc, hastaUtc),
            TopProfesionales = await _reportRepo.GetTopProfesionalesAsync(sucursalId, desdeUtc, hastaUtc),
            Clientes         = await _reportRepo.GetClientesAsync(sucursalId, desdeUtc, hastaUtc),
        });
    }
}
```

**Nota sobre fechas:** El controller recibe `hasta` y le agrega `.AddDays(1).AddTicks(-1)` para incluir el día completo (hasta las 23:59:59.9999...).

---

## Interfaz del repositorio

```csharp
public interface IReportRepository
{
    Task<ReporteTurnosDto>            GetTurnosAsync(Guid sucursalId, DateTime desde, DateTime hasta);
    Task<ReporteIngresosDto>          GetIngresosAsync(Guid sucursalId, DateTime desde, DateTime hasta);
    Task<List<ReporteServicioDto>>    GetTopServiciosAsync(Guid sucursalId, DateTime desde, DateTime hasta, int top = 10);
    Task<List<ReporteProfesionalDto>> GetTopProfesionalesAsync(Guid sucursalId, DateTime desde, DateTime hasta, int top = 10);
    Task<ReporteClientesDto>          GetClientesAsync(Guid sucursalId, DateTime desde, DateTime hasta);
}
```

---

## Implementación del repositorio

### `GetTurnosAsync`

Trae solo los `EstadoTurno` de los turnos del período y los agrupa en memoria.

```csharp
var turnos = await _context.Turnos
    .Where(t => t.SucursalId == sucursalId
             && t.FechaHoraInicio >= desde
             && t.FechaHoraInicio <= hasta)
    .Select(t => t.Estado)
    .ToListAsync();
```

**Mapeo de estados:**
| Campo DTO | Estados incluidos |
|---|---|
| `Completados` | `EstadoTurno.Completado` |
| `Cancelados` | `CanceladoCliente` + `CanceladoStaff` |
| `NoShow` | `EstadoTurno.NoShow` |
| `Pendientes` | `Pendiente` + `Confirmado` |
| `PendientePago` | `EstadoTurno.PendientePago` |

---

### `GetIngresosAsync`

Fuente de datos: `MovimientosCaja`, no `Turnos`. Solo considera movimientos de tipo `CobroTurno` e `IngresoManual`.

```csharp
// 1. Busca la caja de la sucursal
var cajaId = await _context.Cajas
    .Where(c => c.SucursalId == sucursalId)
    .Select(c => c.Id)
    .FirstOrDefaultAsync();

// 2. Filtra movimientos de ingreso en el período
var movimientos = await _context.MovimientosCaja
    .Where(m => m.CajaId == cajaId
             && (m.TipoMovimiento == TipoMovimientoCaja.CobroTurno
                 || m.TipoMovimiento == TipoMovimientoCaja.IngresoManual)
             && m.Fecha >= desdeUtc
             && m.Fecha <= hastaUtc)
    .Select(m => new { m.Monto, m.MedioPago, m.Fecha })
    .ToListAsync();

// 3. Agrupa por día (en memoria)
var porDia = movimientos
    .GroupBy(m => DateOnly.FromDateTime(m.Fecha))
    .Select(g => new IngresosPorDiaDto { Fecha = g.Key, Monto = g.Sum(m => m.Monto) })
    .OrderBy(d => d.Fecha)
    .ToList();
```

**Medios de pago disponibles:**
| Campo DTO | `MedioPago` enum |
|---|---|
| `TotalEfectivo` | `MedioPago.Efectivo` |
| `TotalTransferencia` | `MedioPago.Transferencia` |
| `TotalTarjeta` | `MedioPago.Tarjeta` |
| `TotalMercadoPago` | `MedioPago.MercadoPago` |

> **Nota:** El frontend muestra solo Efectivo y MercadoPago, pero el backend devuelve los 4 medios de pago. Los campos `TotalTransferencia` y `TotalTarjeta` están disponibles si se necesitan en el futuro.

---

### `GetTopServiciosAsync`

Solo considera turnos con `EstadoTurno.Completado`. Agrupa por `ServicioId` y ordena por cantidad de turnos descendente.

```csharp
var datos = await _context.Turnos
    .Where(t => t.SucursalId == sucursalId
             && t.FechaHoraInicio >= desde
             && t.FechaHoraInicio <= hasta
             && t.Estado == EstadoTurno.Completado)
    .Select(t => new { t.ServicioId, ServicioNombre = t.Servicio.Nombre, t.TotalCobrado })
    .ToListAsync();

return datos
    .GroupBy(t => new { t.ServicioId, t.ServicioNombre })
    .Select(g => new ReporteServicioDto { ... CantidadTurnos = g.Count(), Ingresos = g.Sum(...) })
    .OrderByDescending(r => r.CantidadTurnos)
    .Take(top)   // default: 10
    .ToList();
```

---

### `GetTopProfesionalesAsync`

Idéntica lógica que `GetTopServiciosAsync` pero agrupada por `ProfesionalId`. El nombre se arma concatenando `Profesional.Nombre + " " + Profesional.Apellido`.

---

### `GetClientesAsync`

Clasifica clientes del período como **nuevos** o **recurrentes**:

```
Clientes únicos en el período
    → ¿Tuvieron turno ANTES del período?
        Sí → Recurrentes
        No → Nuevos
```

```csharp
// Clientes únicos que tuvieron al menos 1 turno en el período
var clientesPeriodo = await _context.Turnos
    .Where(t => t.SucursalId == sucursalId && t.FechaHoraInicio >= desde && t.FechaHoraInicio <= hasta)
    .Select(t => t.ClienteId)
    .Distinct()
    .ToListAsync();

// De esos, cuántos ya habían venido antes
var recurrentes = await _context.Turnos
    .Where(t => t.SucursalId == sucursalId
             && t.FechaHoraInicio < desde
             && clientesPeriodo.Contains(t.ClienteId))
    .Select(t => t.ClienteId)
    .Distinct()
    .CountAsync();

var nuevos = clientesPeriodo.Count - recurrentes;
```

---

## DTOs

```csharp
// Resumen raíz
public class ReporteResumenDto
{
    public ReporteTurnosDto            Turnos           { get; set; }
    public ReporteIngresosDto          Ingresos         { get; set; }
    public List<ReporteServicioDto>    TopServicios     { get; set; }
    public List<ReporteProfesionalDto> TopProfesionales { get; set; }
    public ReporteClientesDto          Clientes         { get; set; }
}

public class ReporteTurnosDto
{
    public int Total          { get; set; }
    public int Completados    { get; set; }
    public int Cancelados     { get; set; }
    public int NoShow         { get; set; }
    public int Pendientes     { get; set; }
    public int PendientePago  { get; set; }
}

public class ReporteIngresosDto
{
    public decimal TotalCobrado       { get; set; }
    public decimal TotalEfectivo      { get; set; }
    public decimal TotalTransferencia { get; set; }
    public decimal TotalTarjeta       { get; set; }
    public decimal TotalMercadoPago   { get; set; }
    public List<IngresosPorDiaDto> PorDia { get; set; }
}

public class IngresosPorDiaDto
{
    public DateOnly Fecha  { get; set; }
    public decimal  Monto  { get; set; }
}

public class ReporteServicioDto
{
    public Guid    ServicioId    { get; set; }
    public string  Nombre        { get; set; }
    public int     CantidadTurnos{ get; set; }
    public decimal Ingresos      { get; set; }
}

public class ReporteProfesionalDto
{
    public Guid    ProfesionalId { get; set; }
    public string  Nombre        { get; set; }
    public int     CantidadTurnos{ get; set; }
    public decimal Ingresos      { get; set; }
}

public class ReporteClientesDto
{
    public int TotalNuevos      { get; set; }
    public int TotalRecurrentes { get; set; }
    public int TotalUnicos      { get; set; }
}
```

---

## Enums relevantes

### `EstadoTurno`
```csharp
public enum EstadoTurno
{
    Pendiente        = 0,
    Confirmado       = 1,
    CanceladoCliente = 2,
    CanceladoStaff   = 3,
    Completado       = 4,
    NoShow           = 5,
    Reprogramado     = 6,
    PendientePago    = 7,   // turno online con seña, esperando pago MP
    PagoRechazado    = 8,   // pago en MP rechazado/cancelado
}
```

### `MedioPago`
```csharp
public enum MedioPago
{
    Efectivo      = 0,
    MercadoPago   = 1,
    Transferencia = 2,
    Tarjeta       = 3,
    Otro          = 4,
}
```

### `TipoMovimientoCaja`
```csharp
public enum TipoMovimientoCaja
{
    CobroTurno    = 0,
    Gasto         = 1,
    Retiro        = 2,
    Ajuste        = 3,
    IngresoManual = 4,
}
```

---

## Registro en DI

```csharp
// Program.cs
builder.Services.AddScoped<IReportRepository, ReportRepository>();
```

No hay capa de servicio — el controller consume el repositorio directamente. Es apropiado porque reportes es lectura pura sin efectos secundarios ni lógica de negocio compleja.

---

## Frontend

El frontend consume el endpoint `/api/reportes/resumen` con filtros de fecha libre (date pickers "desde/hasta"). Ver `frontend/turnero-ui/src/pages/admin/Reportes.tsx`.

**Funcionalidades del frontend:**
- Gráfico de barras de ingresos por día (Recharts)
- Desglose por medio de pago: solo Efectivo y MercadoPago (con barra de porcentaje)
- Top servicios y profesionales
- Exportar a **PDF** (generado programáticamente con jsPDF — incluye logo, fechas del filtro, todas las secciones)
- Exportar a **Excel** (xlsx — 4 hojas: Resumen, Ingresos por día, Top servicios, Top profesionales)
- Nombre del archivo Excel: `NombreNegocio-MM-YYYY.xlsx`
- Nombre del archivo PDF: `NombreNegocio-DD/MM/YYYY—DD/MM/YYYY.pdf`

---

## Consideraciones de rendimiento

- `GetTurnosAsync` trae solo el campo `Estado` (no entidades completas) → query liviana.
- `GetIngresosAsync` agrupa por día **en memoria** después de traer los movimientos. Aceptable para rangos normales (< 1 año). Si el volumen crece, considerar `GroupBy` en SQL.
- `GetClientesAsync` usa `Contains()` sobre la lista de clientesPeriodo → EF Core lo traduce a `IN (...)`. Si hay miles de clientes únicos en el período, considerar reescribir como subquery.
- `GetTopServiciosAsync` y `GetTopProfesionalesAsync` agrupa en memoria. Misma consideración que ingresos.
- Todos los métodos filtran por `SucursalId` explícitamente (además del query filter global de EF Core).
