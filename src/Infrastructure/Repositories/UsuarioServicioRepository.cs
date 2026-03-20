using Microsoft.EntityFrameworkCore;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;
using TurneroApp.Infrastructure.Data;

namespace TurneroApp.Infrastructure.Repositories;

public class UsuarioServicioRepository : IUsuarioServicioRepository
{
    private readonly AppDbContext _context;

    public UsuarioServicioRepository(AppDbContext context) => _context = context;

    public Task<List<UsuarioServicio>> GetByUsuarioIdAsync(Guid usuarioId) =>
        _context.UsuarioServicios
            .Where(us => us.UsuarioId == usuarioId)
            .ToListAsync();

    public async Task<Dictionary<Guid, List<UsuarioServicio>>> GetByUsuariosAsync(IEnumerable<Guid> usuarioIds)
    {
        var ids = usuarioIds.ToList();
        var lista = await _context.UsuarioServicios
            .Where(us => ids.Contains(us.UsuarioId))
            .ToListAsync();
        return lista
            .GroupBy(us => us.UsuarioId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    public async Task ReplaceAsync(Guid usuarioId, IEnumerable<Guid> servicioIds)
    {
        var existentes = await _context.UsuarioServicios
            .Where(us => us.UsuarioId == usuarioId)
            .ToListAsync();

        _context.UsuarioServicios.RemoveRange(existentes);

        var nuevos = servicioIds.Select(sId => new UsuarioServicio
        {
            UsuarioId  = usuarioId,
            ServicioId = sId,
        });
        await _context.UsuarioServicios.AddRangeAsync(nuevos);
    }

    public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();
}
