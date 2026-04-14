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
            .Where(s => s.SucursalId == sucursalId && (incluirInactivos || s.Activo))
            .OrderBy(s => s.Nombre)
            .ToListAsync();
    }

    public async Task<int> ContarActivosBySucursalAsync(Guid sucursalId)
        => await _dbSet.CountAsync(s => s.SucursalId == sucursalId && s.Activo);
}
