using Microsoft.EntityFrameworkCore;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;
using TurneroApp.Infrastructure.Data;

namespace TurneroApp.Infrastructure.Repositories;

public class ServicioRepository : GenericRepository<Servicio>, IServicioRepository
{
    public ServicioRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Servicio>> GetBySucursalAsync(Guid sucursalId, bool incluirInactivos = false)
    {
        return await _dbSet
            .Include(s => s.DiasDisponibles)
            .Where(s => s.SucursalId == sucursalId && (incluirInactivos || s.Activo))
            .OrderBy(s => s.Nombre)
            .ToListAsync();
    }

    public async Task<Servicio?> GetByIdConDiasAsync(Guid id)
    {
        return await _dbSet
            .Include(s => s.DiasDisponibles)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task ActualizarDiasDisponiblesAsync(Guid servicioId, IEnumerable<DayOfWeek> dias)
    {
        var existentes = await _context.Set<ServicioDiaDisponible>()
            .Where(d => d.ServicioId == servicioId)
            .ToListAsync();

        _context.Set<ServicioDiaDisponible>().RemoveRange(existentes);

        foreach (var dia in dias.Distinct())
        {
            await _context.Set<ServicioDiaDisponible>().AddAsync(new ServicioDiaDisponible
            {
                ServicioId = servicioId,
                DiaSemana  = dia,
            });
        }
    }

    public async Task<int> ContarActivosBySucursalAsync(Guid sucursalId)
        => await _dbSet.CountAsync(s => s.SucursalId == sucursalId && s.Activo);
}
