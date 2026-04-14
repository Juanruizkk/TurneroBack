using Microsoft.EntityFrameworkCore;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;
using TurneroApp.Infrastructure.Data;

namespace TurneroApp.Infrastructure.Repositories;

public class HorarioLaboralRepository : IHorarioLaboralRepository
{
    private readonly AppDbContext _context;

    public HorarioLaboralRepository(AppDbContext context) => _context = context;

    /// <inheritdoc />
    public Task<List<HorarioLaboral>> GetByUsuarioIdAsync(Guid usuarioId) =>
        _context.HorariosLaborales
            .Where(h => h.UsuarioSucursalRol.UsuarioId == usuarioId)
            .ToListAsync();

    /// <inheritdoc />
    public async Task<Dictionary<Guid, List<HorarioLaboral>>> GetByUsuariosAsync(IEnumerable<Guid> usuarioIds)
    {
        var ids = usuarioIds.ToList();
        var lista = await _context.HorariosLaborales
            .Include(h => h.UsuarioSucursalRol)
            .Where(h => ids.Contains(h.UsuarioSucursalRol.UsuarioId))
            .ToListAsync();
        return lista
            .GroupBy(h => h.UsuarioSucursalRol.UsuarioId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <inheritdoc />
    public async Task ReplaceAsync(Guid usuarioSucursalRolId, IEnumerable<HorarioLaboral> nuevos)
    {
        var existentes = await _context.HorariosLaborales
            .Where(h => h.UsuarioSucursalRolId == usuarioSucursalRolId)
            .ToListAsync();

        _context.HorariosLaborales.RemoveRange(existentes);
        await _context.HorariosLaborales.AddRangeAsync(nuevos);
    }

    public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();
}
