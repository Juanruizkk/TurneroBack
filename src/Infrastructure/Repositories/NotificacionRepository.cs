using Microsoft.EntityFrameworkCore;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;
using TurneroApp.Infrastructure.Data;

namespace TurneroApp.Infrastructure.Repositories;

public class NotificacionRepository : INotificacionRepository
{
    private readonly AppDbContext _context;

    public NotificacionRepository(AppDbContext context) => _context = context;

    public async Task AddAsync(Notificacion notificacion)
        => await _context.Notificaciones.AddAsync(notificacion);

    public Task<List<Notificacion>> GetNoLeidasBySucursalAsync(Guid sucursalId)
        => _context.Notificaciones
            .Where(n => n.SucursalId == sucursalId && !n.Leida)
            .OrderByDescending(n => n.FechaCreacion)
            .Take(50)
            .ToListAsync();

    public Task MarcarTodasLeidasAsync(Guid sucursalId)
        => _context.Notificaciones
            .Where(n => n.SucursalId == sucursalId && !n.Leida)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.Leida, true));

    public Task EliminarBySucursalAsync(Guid sucursalId)
        => _context.Notificaciones
            .Where(n => n.SucursalId == sucursalId)
            .ExecuteDeleteAsync();

    public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();
}
