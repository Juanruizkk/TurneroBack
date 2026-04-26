using Microsoft.EntityFrameworkCore;
using TurneroApp.Application.DTOs.Cliente;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;
using TurneroApp.Infrastructure.Data;

namespace TurneroApp.Infrastructure.Repositories;

public class ClienteRepository : GenericRepository<Cliente>, IClienteRepository
{
    public ClienteRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Cliente>> GetBySucursalAsync(Guid sucursalId)
    {
        return await _dbSet
            .Include(c => c.Turnos)
            .Where(c => c.SucursalId == sucursalId)
            .OrderBy(c => c.Apellido)
            .ThenBy(c => c.Nombre)
            .ToListAsync();
    }

    public async Task<Cliente?> BuscarPorTelefonoAsync(Guid sucursalId, string telefono)
    {
        return await _dbSet
            .FirstOrDefaultAsync(c => c.SucursalId == sucursalId && c.Telefono == telefono);
    }

    public async Task<Cliente?> BuscarPorEmailAsync(Guid sucursalId, string email)
    {
        return await _dbSet
            .FirstOrDefaultAsync(c => c.SucursalId == sucursalId
                                   && c.Email != null
                                   && c.Email.ToLower() == email.ToLower());
    }

    public async Task<IEnumerable<Cliente>> BuscarAsync(Guid sucursalId, string q, int max = 10)
    {
        var ql = q.ToLower();
        return await _dbSet
            .Where(c => c.SucursalId == sucursalId && (
                c.Nombre.ToLower().Contains(ql) ||
                c.Apellido.ToLower().Contains(ql) ||
                c.Telefono.Contains(q) ||
                (c.Email != null && c.Email.ToLower().Contains(ql))
            ))
            .OrderBy(c => c.Apellido)
            .ThenBy(c => c.Nombre)
            .Take(max)
            .ToListAsync();
    }

    public async Task<Cliente?> GetByIdIgnorandoFiltrosAsync(Guid id)
        => await _dbSet.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == id);

    public async Task<IEnumerable<Cliente>> GetArchivedBySucursalAsync(Guid sucursalId)
        => await _dbSet.IgnoreQueryFilters()
            .Where(c => c.SucursalId == sucursalId && !c.Activo)
            .OrderByDescending(c => c.FechaArchivado)
            .ToListAsync();

    public async Task<bool> TelefonoExisteEnSucursalAsync(Guid sucursalId, string telefono, Guid? excludeId = null)
        => await _dbSet.IgnoreQueryFilters()
            .AnyAsync(c => c.SucursalId == sucursalId
                        && c.Telefono == telefono
                        && (excludeId == null || c.Id != excludeId));

    public async Task<bool> TieneImpactoOperacionalAsync(Guid clienteId)
    {
        var estadosBloqueantes = new[]
        {
            EstadoTurno.Pendiente, EstadoTurno.Confirmado,
            EstadoTurno.Completado, EstadoTurno.NoShow,
            EstadoTurno.Reprogramado
        };

        var tieneTurnosBloqueantes = await _context.Turnos
            .IgnoreQueryFilters()
            .AnyAsync(t => t.ClienteId == clienteId && estadosBloqueantes.Contains(t.Estado));

        if (tieneTurnosBloqueantes) return true;

        return await _context.PagosTurno
            .IgnoreQueryFilters()
            .AnyAsync(p => p.Turno.ClienteId == clienteId);
    }

    public async Task EliminarTurnosNoCriticosAsync(Guid clienteId)
    {
        // Elimina turnos en estados sin impacto operacional (pago rechazado, pendiente de pago, cancelados).
        // Comentarios y Adjuntos se eliminan en cascada a nivel DB (DeleteBehavior.Cascade).
        var estadosNoCriticos = new[]
        {
            EstadoTurno.PagoRechazado, EstadoTurno.PendientePago,
            EstadoTurno.CanceladoCliente, EstadoTurno.CanceladoStaff
        };

        await _context.Turnos
            .IgnoreQueryFilters()
            .Where(t => t.ClienteId == clienteId && estadosNoCriticos.Contains(t.Estado))
            .ExecuteDeleteAsync();
    }

    public async Task<int> PurgarClientesFantasmaAsync(IEnumerable<Guid> clienteIds)
    {
        var estadosBloqueantes = new[]
        {
            EstadoTurno.Pendiente, EstadoTurno.Confirmado,
            EstadoTurno.Completado, EstadoTurno.NoShow,
            EstadoTurno.Reprogramado
        };

        var estadosNoCriticos = new[]
        {
            EstadoTurno.PagoRechazado, EstadoTurno.PendientePago,
            EstadoTurno.CanceladoCliente, EstadoTurno.CanceladoStaff
        };

        int eliminados = 0;

        foreach (var clienteId in clienteIds)
        {
            var tieneImpacto =
                await _context.Turnos.IgnoreQueryFilters()
                    .AnyAsync(t => t.ClienteId == clienteId && estadosBloqueantes.Contains(t.Estado))
                || await _context.PagosTurno.IgnoreQueryFilters()
                    .AnyAsync(p => p.Turno.ClienteId == clienteId);

            if (tieneImpacto) continue;

            // Borrar turnos no críticos — Comentarios y Adjuntos se eliminan en cascada (DB)
            await _context.Turnos.IgnoreQueryFilters()
                .Where(t => t.ClienteId == clienteId && estadosNoCriticos.Contains(t.Estado))
                .ExecuteDeleteAsync();

            var cliente = await _dbSet.IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == clienteId);

            if (cliente is null) continue;

            _dbSet.Remove(cliente);
            eliminados++;
        }

        if (eliminados > 0)
            await _context.SaveChangesAsync();

        return eliminados;
    }

    public async Task<ClienteFichaDto?> GetFichaAsync(Guid clienteId)
    {
        var cliente = await _dbSet
            .IgnoreQueryFilters()
            .Include(c => c.Turnos)
                .ThenInclude(t => t.Servicio)
            .Include(c => c.Turnos)
                .ThenInclude(t => t.Profesional)
            .Include(c => c.Turnos)
                .ThenInclude(t => t.Pagos)
            .FirstOrDefaultAsync(c => c.Id == clienteId);

        if (cliente is null) return null;

        var ahora = DateTime.UtcNow;
        var turnos = cliente.Turnos.OrderByDescending(t => t.FechaHoraInicio).ToList();

        const int UMBRAL_FRECUENTE = 5;

        return new ClienteFichaDto
        {
            Id             = cliente.Id,
            Nombre         = cliente.Nombre,
            Apellido       = cliente.Apellido,
            Telefono       = cliente.Telefono,
            Email          = cliente.Email,
            Observaciones  = cliente.Observaciones,
            Activo         = cliente.Activo,
            FechaArchivado = cliente.FechaArchivado,

            TotalTurnos           = turnos.Count,
            CantidadNoShow        = turnos.Count(t => t.Estado == EstadoTurno.NoShow),
            CantidadCancelaciones = turnos.Count(t => t.Estado == EstadoTurno.CanceladoCliente
                                                   || t.Estado == EstadoTurno.CanceladoStaff),
            UltimoTurno  = turnos.Where(t => t.FechaHoraInicio <= ahora)
                                  .Select(t => t.FechaHoraInicio)
                                  .Cast<DateTime?>()
                                  .FirstOrDefault(),
            ProximoTurno = turnos.Where(t => t.FechaHoraInicio > ahora)
                                  .OrderBy(t => t.FechaHoraInicio)
                                  .Select(t => t.FechaHoraInicio)
                                  .Cast<DateTime?>()
                                  .FirstOrDefault(),
            TotalCobrado = turnos.Sum(t => t.TotalCobrado),
            EsFrecuente  = turnos.Count > UMBRAL_FRECUENTE,
            TieneNoShow  = turnos.Any(t => t.Estado == EstadoTurno.NoShow),

            Historial = turnos
                .Where(t => t.FechaHoraInicio <= ahora)
                .Select(t => new ClienteTurnoHistorialDto
                {
                    TurnoId        = t.Id,
                    Fecha          = t.FechaHoraInicio,
                    Servicio       = t.Servicio.Nombre,
                    Profesional    = $"{t.Profesional.Nombre} {t.Profesional.Apellido}".Trim(),
                    Estado         = t.Estado.ToString(),
                    PrecioServicio = t.PrecioServicio,
                    TotalCobrado   = t.TotalCobrado,
                    TuvoSena       = t.MontoSena.HasValue && t.MontoSena > 0,
                    TokenGestion   = t.TokenGestion,
                }).ToList(),

            ProximosTurnos = turnos
                .Where(t => t.FechaHoraInicio > ahora)
                .OrderBy(t => t.FechaHoraInicio)
                .Select(t => new ClienteProximoTurnoDto
                {
                    TurnoId     = t.Id,
                    Fecha       = t.FechaHoraInicio,
                    Servicio    = t.Servicio.Nombre,
                    Profesional = $"{t.Profesional.Nombre} {t.Profesional.Apellido}".Trim(),
                    Estado      = t.Estado.ToString(),
                }).ToList(),
        };
    }
}
