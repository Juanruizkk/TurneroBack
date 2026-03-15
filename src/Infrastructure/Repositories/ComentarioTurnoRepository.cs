using Microsoft.EntityFrameworkCore;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;
using TurneroApp.Infrastructure.Data;

namespace TurneroApp.Infrastructure.Repositories;

public class ComentarioTurnoRepository : IComentarioTurnoRepository
{
    private readonly AppDbContext _context;

    public ComentarioTurnoRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<ComentarioTurno>> GetByTurnoIdAsync(Guid turnoId)
    {
        return await _context.ComentariosTurno
            .Include(c => c.Autor)
            .Where(c => c.TurnoId == turnoId)
            .OrderBy(c => c.Fecha)
            .ToListAsync();
    }

    public async Task<ComentarioTurno?> GetByIdAsync(Guid id)
    {
        return await _context.ComentariosTurno
            .Include(c => c.Autor)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task AddAsync(ComentarioTurno comentario)
    {
        await _context.ComentariosTurno.AddAsync(comentario);
    }

    public void Delete(ComentarioTurno comentario)
    {
        _context.ComentariosTurno.Remove(comentario);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
