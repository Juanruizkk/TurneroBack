using Microsoft.EntityFrameworkCore;
using TurneroApp.Application.DTOs.Reportes;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Enums;
using TurneroApp.Infrastructure.Data;

namespace TurneroApp.Infrastructure.Repositories;

public class ReportRepository : IReportRepository
{
    private readonly AppDbContext _context;

    public ReportRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ReporteTurnosDto> GetTurnosAsync(Guid sucursalId, DateTime desde, DateTime hasta)
    {
        var turnos = await _context.Turnos
            .Where(t => t.SucursalId == sucursalId
                     && t.FechaHoraInicio >= desde
                     && t.FechaHoraInicio <= hasta)
            .Select(t => t.Estado)
            .ToListAsync();

        return new ReporteTurnosDto
        {
            Total         = turnos.Count,
            Completados   = turnos.Count(e => e == EstadoTurno.Completado),
            Cancelados    = turnos.Count(e => e == EstadoTurno.CanceladoCliente || e == EstadoTurno.CanceladoStaff),
            NoShow        = turnos.Count(e => e == EstadoTurno.NoShow),
            Pendientes    = turnos.Count(e => e == EstadoTurno.Pendiente || e == EstadoTurno.Confirmado),
            PendientePago = turnos.Count(e => e == EstadoTurno.PendientePago),
        };
    }

    public async Task<ReporteIngresosDto> GetIngresosAsync(Guid sucursalId, DateTime desde, DateTime hasta)
    {
        // Los movimientos de la caja de la sucursal en el período
        var caja = await _context.Cajas
            .Where(c => c.SucursalId == sucursalId)
            .Select(c => c.Id)
            .FirstOrDefaultAsync();

        if (caja == default)
            return new ReporteIngresosDto();

        // MovimientoCaja.Fecha es timestamptz → Npgsql requiere Kind=Utc
        var desdeUtc = DateTime.SpecifyKind(desde, DateTimeKind.Utc);
        var hastaUtc = DateTime.SpecifyKind(hasta, DateTimeKind.Utc);

        var movimientos = await _context.MovimientosCaja
            .Where(m => m.CajaId == caja
                     && (m.TipoMovimiento == TipoMovimientoCaja.CobroTurno
                         || m.TipoMovimiento == TipoMovimientoCaja.IngresoManual)
                     && m.Fecha >= desdeUtc
                     && m.Fecha <= hastaUtc)
            .Select(m => new { m.Monto, m.MedioPago, m.Fecha })
            .ToListAsync();

        var porDia = movimientos
            .GroupBy(m => DateOnly.FromDateTime(m.Fecha))
            .Select(g => new IngresosPorDiaDto { Fecha = g.Key, Monto = g.Sum(m => m.Monto) })
            .OrderBy(d => d.Fecha)
            .ToList();

        return new ReporteIngresosDto
        {
            TotalCobrado       = movimientos.Sum(m => m.Monto),
            TotalEfectivo      = movimientos.Where(m => m.MedioPago == MedioPago.Efectivo).Sum(m => m.Monto),
            TotalTransferencia = movimientos.Where(m => m.MedioPago == MedioPago.Transferencia).Sum(m => m.Monto),
            TotalTarjeta       = movimientos.Where(m => m.MedioPago == MedioPago.Tarjeta).Sum(m => m.Monto),
            TotalMercadoPago   = movimientos.Where(m => m.MedioPago == MedioPago.MercadoPago).Sum(m => m.Monto),
            PorDia             = porDia,
        };
    }

    public async Task<List<ReporteServicioDto>> GetTopServiciosAsync(
        Guid sucursalId, DateTime desde, DateTime hasta, int top = 10)
    {
        var datos = await _context.Turnos
            .Where(t => t.SucursalId == sucursalId
                     && t.FechaHoraInicio >= desde
                     && t.FechaHoraInicio <= hasta
                     && t.Estado == EstadoTurno.Completado)
            .Select(t => new
            {
                t.ServicioId,
                ServicioNombre = t.Servicio.Nombre,
                t.TotalCobrado,
            })
            .ToListAsync();

        return datos
            .GroupBy(t => new { t.ServicioId, t.ServicioNombre })
            .Select(g => new ReporteServicioDto
            {
                ServicioId     = g.Key.ServicioId,
                Nombre         = g.Key.ServicioNombre,
                CantidadTurnos = g.Count(),
                Ingresos       = g.Sum(t => t.TotalCobrado),
            })
            .OrderByDescending(r => r.CantidadTurnos)
            .Take(top)
            .ToList();
    }

    public async Task<List<ReporteProfesionalDto>> GetTopProfesionalesAsync(
        Guid sucursalId, DateTime desde, DateTime hasta, int top = 10)
    {
        var datos = await _context.Turnos
            .Where(t => t.SucursalId == sucursalId
                     && t.FechaHoraInicio >= desde
                     && t.FechaHoraInicio <= hasta
                     && t.Estado == EstadoTurno.Completado)
            .Select(t => new
            {
                t.ProfesionalId,
                ProfesionalNombre = t.Profesional.Nombre + " " + t.Profesional.Apellido,
                t.TotalCobrado,
            })
            .ToListAsync();

        return datos
            .GroupBy(t => new { t.ProfesionalId, t.ProfesionalNombre })
            .Select(g => new ReporteProfesionalDto
            {
                ProfesionalId  = g.Key.ProfesionalId,
                Nombre         = g.Key.ProfesionalNombre,
                CantidadTurnos = g.Count(),
                Ingresos       = g.Sum(t => t.TotalCobrado),
            })
            .OrderByDescending(r => r.CantidadTurnos)
            .Take(top)
            .ToList();
    }

    public async Task<ReporteClientesDto> GetClientesAsync(Guid sucursalId, DateTime desde, DateTime hasta)
    {
        // Clientes únicos con al menos un turno en el período
        var clientesPeriodo = await _context.Turnos
            .Where(t => t.SucursalId == sucursalId
                     && t.FechaHoraInicio >= desde
                     && t.FechaHoraInicio <= hasta)
            .Select(t => t.ClienteId)
            .Distinct()
            .ToListAsync();

        // Recurrentes: tuvieron turno ANTES del período y también dentro
        var recurrentes = 0;
        if (clientesPeriodo.Count > 0)
        {
            recurrentes = await _context.Turnos
                .Where(t => t.SucursalId == sucursalId
                         && t.FechaHoraInicio < desde
                         && clientesPeriodo.Contains(t.ClienteId))
                .Select(t => t.ClienteId)
                .Distinct()
                .CountAsync();
        }

        // Nuevos: tuvieron turno en el período pero NINGUNO antes
        var nuevos = clientesPeriodo.Count - recurrentes;

        return new ReporteClientesDto
        {
            TotalNuevos      = nuevos < 0 ? 0 : nuevos,
            TotalRecurrentes = recurrentes,
            TotalUnicos      = clientesPeriodo.Count,
        };
    }
}
