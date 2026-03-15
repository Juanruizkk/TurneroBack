using Microsoft.EntityFrameworkCore;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;
using TurneroApp.Infrastructure.Data;

namespace TurneroApp.Infrastructure.Repositories;

public class SucursalRepository : GenericRepository<Sucursal>, ISucursalRepository
{
    public SucursalRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Sucursal>> GetActivasAsync()
    {
        return await _dbSet
            .Where(s => s.Activa)
            .OrderBy(s => s.Nombre)
            .ToListAsync();
    }

    public async Task<IEnumerable<Sucursal>> GetAllConOwnerAsync()
    {
        return await _dbSet
            .Include(s => s.Usuarios.Where(u => u.EsOwner))
            .OrderByDescending(s => s.FechaCreacion)
            .ToListAsync();
    }

    public async Task<Sucursal?> GetByIdConOwnerAsync(Guid id)
    {
        return await _dbSet
            .Include(s => s.Usuarios.Where(u => u.EsOwner))
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<Sucursal?> GetByIdConConfiguracionAsync(Guid id)
    {
        return await _dbSet
            .Include(s => s.ConfiguracionSucursal)
            .FirstOrDefaultAsync(s => s.Id == id);
    }
}
