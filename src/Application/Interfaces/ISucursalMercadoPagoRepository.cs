using TurneroApp.Domain.Entities;

namespace TurneroApp.Application.Interfaces;

public interface ISucursalMercadoPagoRepository : IGenericRepository<SucursalMercadoPago>
{
    Task<SucursalMercadoPago?> GetBySucursalIdAsync(Guid sucursalId);
}
