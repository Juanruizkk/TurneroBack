using Microsoft.EntityFrameworkCore;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;
using TurneroApp.Infrastructure.Data;

namespace TurneroApp.Infrastructure.Repositories;

public class SucursalMercadoPagoRepository : GenericRepository<SucursalMercadoPago>, ISucursalMercadoPagoRepository
{
    public SucursalMercadoPagoRepository(AppDbContext context) : base(context) { }

    public async Task<SucursalMercadoPago?> GetBySucursalIdAsync(Guid sucursalId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(e => e.SucursalId == sucursalId);
    }
}
