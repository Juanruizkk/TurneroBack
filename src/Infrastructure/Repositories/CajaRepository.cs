using Microsoft.EntityFrameworkCore;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;
using TurneroApp.Infrastructure.Data;

namespace TurneroApp.Infrastructure.Repositories;

public class CajaRepository : GenericRepository<Caja>, ICajaRepository
{
    public CajaRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<Caja?> GetBySucursalAsync(Guid sucursalId)
        => await _dbSet.FirstOrDefaultAsync(c => c.SucursalId == sucursalId);

    public async Task<IEnumerable<MovimientoCaja>> GetMovimientosByCajaAsync(
        Guid cajaId, DateTime? desde = null, DateTime? hasta = null)
    {
        var query = _context.MovimientosCaja
            .Include(m => m.Usuario)
            .Include(m => m.PagoTurno)
            .Include(m => m.Turno)
                .ThenInclude(t => t!.Cliente)
            .Include(m => m.Turno)
                .ThenInclude(t => t!.Servicio)
            .Include(m => m.Turno)
                .ThenInclude(t => t!.Profesional)
            .Where(m => m.CajaId == cajaId);

        if (desde.HasValue)
        {
            var desdeUtc = DateTime.SpecifyKind(desde.Value.Date, DateTimeKind.Utc);
            query = query.Where(m => m.Fecha >= desdeUtc);
        }
        if (hasta.HasValue)
        {
            // Incluir todo el día de 'hasta'
            var hastaUtc = DateTime.SpecifyKind(hasta.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
            query = query.Where(m => m.Fecha <= hastaUtc);
        }

        return await query.OrderByDescending(m => m.Fecha).ToListAsync();
    }

    public async Task AddMovimientoAsync(MovimientoCaja movimiento)
        => await _context.MovimientosCaja.AddAsync(movimiento);

    public async Task<MovimientoCaja> RegistrarMovimientoTransaccionalAsync(
        MovimientoCaja movimiento,
        PagoTurno?     pagoTurno,
        Turno?         turnoAActualizar)
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

            if (pagoTurno is not null)
                await _context.PagosTurno.AddAsync(pagoTurno);

            if (turnoAActualizar is not null)
                _context.Turnos.Update(turnoAActualizar);

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            // Cargar la nav prop para que el mapper la encuentre inmediatamente
            if (pagoTurno is not null)
                movimiento.PagoTurno = pagoTurno;
            return movimiento;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<IEnumerable<MovimientoCaja>> GetMovimientosRangoAsync(
        Guid cajaId, DateTime desde, DateTime hasta)
        => await _context.MovimientosCaja
            .Where(m => m.CajaId == cajaId && m.Fecha >= desde && m.Fecha <= hasta)
            .ToListAsync();

    public async Task<IEnumerable<MovimientoCaja>> GetCobrosTurnoConProfesionalAsync(
        Guid cajaId, DateTime desde, DateTime hasta)
        => await _context.MovimientosCaja
            .Include(m => m.Turno)
                .ThenInclude(t => t!.Profesional)
            .Where(m => m.CajaId == cajaId
                     && m.TipoMovimiento == TipoMovimientoCaja.CobroTurno
                     && m.TurnoId != null
                     && m.Fecha >= desde
                     && m.Fecha <= hasta)
            .ToListAsync();

    public async Task<IEnumerable<PagoTurno>> GetPagosByTurnoAsync(Guid turnoId)
        => await _context.PagosTurno
            .Where(p => p.TurnoId == turnoId)
            .OrderBy(p => p.Fecha)
            .ToListAsync();

    // ── Sesiones de caja ──────────────────────────────────────────────────────

    public async Task<CajaSesion?> GetSesionActivaByCajaAsync(Guid cajaId)
        => await _context.CajaSesiones
            .Include(s => s.UsuarioApertura)
            .FirstOrDefaultAsync(s => s.CajaId == cajaId
                                   && s.Estado == EstadoCajaSesion.Abierta);

    public async Task<CajaSesion> AbrirSesionAsync(CajaSesion sesion)
    {
        await _context.CajaSesiones.AddAsync(sesion);
        await _context.SaveChangesAsync();
        return sesion;
    }

    public async Task<CajaSesion> CerrarSesionAsync(
        Guid    sesionId,
        decimal montoFinalReal,
        decimal montoFinalEsperado,
        Guid?   usuarioCierreId,
        string? observaciones)
    {
        var sesion = await _context.CajaSesiones
            .Include(s => s.UsuarioApertura)
            .Include(s => s.UsuarioCierre)
            .FirstOrDefaultAsync(s => s.Id == sesionId)
            ?? throw new InvalidOperationException($"Sesión con Id {sesionId} no encontrada.");

        sesion.FechaCierre          = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        sesion.MontoFinalReal       = montoFinalReal;
        sesion.MontoFinalEsperado   = montoFinalEsperado;
        sesion.Diferencia           = montoFinalReal - montoFinalEsperado;
        sesion.Estado               = EstadoCajaSesion.Cerrada;
        sesion.UsuarioCierreId      = usuarioCierreId;
        sesion.ObservacionesCierre  = observaciones;

        await _context.SaveChangesAsync();
        return sesion;
    }

    public async Task<IEnumerable<CajaSesion>> GetSesionesByCajaAsync(Guid cajaId)
        => await _context.CajaSesiones
            .Include(s => s.UsuarioApertura)
            .Include(s => s.UsuarioCierre)
            .Where(s => s.CajaId == cajaId)
            .OrderByDescending(s => s.FechaApertura)
            .ToListAsync();
}
