using TurneroApp.Domain.Entities;

namespace TurneroApp.Application.Interfaces;

public interface IServicioRepository : IGenericRepository<Servicio>
{
    Task<IEnumerable<Servicio>> GetBySucursalAsync(Guid sucursalId, bool incluirInactivos = false);
}
