using Microsoft.EntityFrameworkCore;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;
using TurneroApp.Infrastructure.Data;

namespace TurneroApp.Infrastructure.Repositories;

public class TurnoRepository : GenericRepository<Turno>, ITurnoRepository
{
    public TurnoRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<Turno?> GetByIdConRelacionesAsync(Guid id)
    {
        return await _dbSet
            .Include(t => t.Cliente)
            .Include(t => t.Servicio)
            .Include(t => t.Profesional)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<IEnumerable<Turno>> GetBySucursalAsync(Guid sucursalId)
    {
        return await _dbSet
            .Include(t => t.Cliente)
            .Include(t => t.Servicio)
            .Include(t => t.Profesional)
            .Where(t => t.SucursalId == sucursalId)
            .OrderBy(t => t.FechaHoraInicio)
            .ToListAsync();
    }

    public async Task<IEnumerable<Turno>> GetByProfesionalAsync(
        Guid profesionalId, DateTime? desde = null, DateTime? hasta = null)
    {
        var query = _dbSet
            .Include(t => t.Cliente)
            .Include(t => t.Servicio)
            .Where(t => t.ProfesionalId == profesionalId);

        if (desde.HasValue)
            query = query.Where(t => t.FechaHoraInicio >= desde.Value);

        if (hasta.HasValue)
            query = query.Where(t => t.FechaHoraInicio <= hasta.Value);

        return await query
            .OrderBy(t => t.FechaHoraInicio)
            .ToListAsync();
    }

    public async Task<IEnumerable<Turno>> GetByClienteAsync(Guid clienteId)
    {
        return await _dbSet
            .Include(t => t.Servicio)
            .Include(t => t.Profesional)
            .Where(t => t.ClienteId == clienteId)
            .OrderByDescending(t => t.FechaHoraInicio)
            .ToListAsync();
    }

    public async Task<Turno?> GetByTokenAsync(string token)
    {
        return await _dbSet
            .Include(t => t.Cliente)
            .Include(t => t.Servicio)
            .Include(t => t.Profesional)
            .FirstOrDefaultAsync(t => t.TokenGestion == token);
    }

    public async Task<IEnumerable<Turno>> GetActivosSucursalEnRangoAsync(
        Guid sucursalId, DateTime desde, DateTime hasta)
    {
        return await _dbSet
            .Where(t =>
                t.SucursalId == sucursalId &&
                t.Estado != EstadoTurno.CanceladoCliente &&
                t.Estado != EstadoTurno.CanceladoStaff &&
                t.Estado != EstadoTurno.NoShow &&
                t.Estado != EstadoTurno.PagoRechazado &&
                t.FechaHoraInicio < hasta &&
                t.FechaHoraFin > desde)
            .ToListAsync();
    }

    public async Task<bool> ExisteConflictoHorarioAsync(
        Guid profesionalId, DateTime inicio, DateTime fin, Guid? excludeTurnoId = null)
    {
        var query = _dbSet.Where(t =>
            t.ProfesionalId == profesionalId &&
            t.Estado != EstadoTurno.CanceladoCliente &&
            t.Estado != EstadoTurno.CanceladoStaff &&
            t.Estado != EstadoTurno.NoShow &&
            t.Estado != EstadoTurno.PagoRechazado &&
            t.FechaHoraInicio < fin &&
            t.FechaHoraFin > inicio);

        if (excludeTurnoId.HasValue)
            query = query.Where(t => t.Id != excludeTurnoId.Value);

        return await query.AnyAsync();
    }

    public async Task<IEnumerable<Turno>> GetConfirmadosPendientesAntesDeAsync(DateTime fechaLimite)
    {
        return await _dbSet
            .Include(t => t.Cliente)
            .Include(t => t.Servicio)
            .Include(t => t.Profesional)
            .Where(t =>
                (t.Estado == EstadoTurno.Confirmado || t.Estado == EstadoTurno.Pendiente) &&
                t.FechaHoraFin < fechaLimite)
            .ToListAsync();
    }

    public async Task<IEnumerable<Turno>> GetPendientePagoVencidosAsync(DateTime limite)
        => await _dbSet
            .Where(t => t.Estado == EstadoTurno.PendientePago && t.FechaCreacion < limite)
            .ToListAsync();

    public async Task<IEnumerable<Turno>> GetParaRecordatoriosAsync(
        DateTime ahora, DateTime limite24h, DateTime limite3h)
    {
        return await _dbSet
            .Include(t => t.Cliente)
            .Include(t => t.Servicio)
            .Include(t => t.Profesional)
            .Include(t => t.Sucursal)
            .Where(t =>
                (t.Estado == EstadoTurno.Confirmado || t.Estado == EstadoTurno.Pendiente) &&
                t.FechaHoraInicio > ahora &&
                (
                    (!t.Recordatorio24hEnviado && t.FechaHoraInicio <= limite24h) ||
                    (!t.Recordatorio3hEnviado  && t.FechaHoraInicio <= limite3h)
                ))
            .OrderBy(t => t.FechaHoraInicio)
            .ToListAsync();
    }
}
