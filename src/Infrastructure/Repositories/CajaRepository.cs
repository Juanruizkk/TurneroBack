using Microsoft.EntityFrameworkCore;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;
using TurneroApp.Infrastructure.Data;

namespace TurneroApp.Infrastructure.Repositories;

public class CajaRepository : GenericRepository<Caja>, ICajaRepository
{
    public CajaRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<Caja?> GetBySucursalAsync(Guid sucursalId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(c => c.SucursalId == sucursalId);
    }

    public async Task<IEnumerable<MovimientoCaja>> GetMovimientosByCajaAsync(
        Guid cajaId, DateTime? desde = null, DateTime? hasta = null)
    {
        var query = _context.MovimientosCaja
            .Include(m => m.Usuario)
            .Where(m => m.CajaId == cajaId);

        if (desde.HasValue)
            query = query.Where(m => m.Fecha >= desde.Value);

        if (hasta.HasValue)
            query = query.Where(m => m.Fecha <= hasta.Value);

        return await query
            .OrderByDescending(m => m.Fecha)
            .ToListAsync();
    }

    public async Task AddMovimientoAsync(MovimientoCaja movimiento)
    {
        await _context.MovimientosCaja.AddAsync(movimiento);
    }
}
