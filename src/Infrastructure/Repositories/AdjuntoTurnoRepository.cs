using Microsoft.EntityFrameworkCore;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;
using TurneroApp.Infrastructure.Data;

namespace TurneroApp.Infrastructure.Repositories;

public class AdjuntoTurnoRepository : IAdjuntoTurnoRepository
{
    private readonly AppDbContext _context;

    public AdjuntoTurnoRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<AdjuntoTurno>> GetByTurnoIdAsync(Guid turnoId)
    {
        return await _context.AdjuntosTurno
            .Where(a => a.TurnoId == turnoId)
            .OrderBy(a => a.Fecha)
            .ToListAsync();
    }

    public async Task<AdjuntoTurno?> GetByIdAsync(Guid id)
    {
        return await _context.AdjuntosTurno.FindAsync(id);
    }

    public async Task AddAsync(AdjuntoTurno adjunto)
    {
        await _context.AdjuntosTurno.AddAsync(adjunto);
    }

    public void Delete(AdjuntoTurno adjunto)
    {
        _context.AdjuntosTurno.Remove(adjunto);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
