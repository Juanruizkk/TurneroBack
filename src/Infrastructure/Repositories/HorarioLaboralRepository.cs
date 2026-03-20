using Microsoft.EntityFrameworkCore;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;
using TurneroApp.Infrastructure.Data;

namespace TurneroApp.Infrastructure.Repositories;

public class HorarioLaboralRepository : IHorarioLaboralRepository
{
    private readonly AppDbContext _context;

    public HorarioLaboralRepository(AppDbContext context) => _context = context;

    public Task<List<HorarioLaboral>> GetByUsuarioIdAsync(Guid usuarioId) =>
        _context.HorariosLaborales
            .Where(h => h.UsuarioId == usuarioId)
            .ToListAsync();

    public async Task<Dictionary<Guid, List<HorarioLaboral>>> GetByUsuariosAsync(IEnumerable<Guid> usuarioIds)
    {
        var ids = usuarioIds.ToList();
        var lista = await _context.HorariosLaborales
            .Where(h => ids.Contains(h.UsuarioId))
            .ToListAsync();
        return lista
            .GroupBy(h => h.UsuarioId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    public async Task ReplaceAsync(Guid usuarioId, IEnumerable<HorarioLaboral> nuevos)
    {
        var existentes = await _context.HorariosLaborales
            .Where(h => h.UsuarioId == usuarioId)
            .ToListAsync();

        _context.HorariosLaborales.RemoveRange(existentes);
        await _context.HorariosLaborales.AddRangeAsync(nuevos);
    }

    public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();
}
