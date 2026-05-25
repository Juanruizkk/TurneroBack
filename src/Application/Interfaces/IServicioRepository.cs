using TurneroApp.Domain.Entities;

namespace TurneroApp.Application.Interfaces;

public interface IServicioRepository : IGenericRepository<Servicio>
{
    Task<IEnumerable<Servicio>> GetBySucursalAsync(Guid sucursalId, bool incluirInactivos = false);
    Task<int> ContarActivosBySucursalAsync(Guid sucursalId);
    Task<Servicio?> GetByIdConDiasAsync(Guid id);
    Task ActualizarDiasDisponiblesAsync(Guid servicioId, IEnumerable<DayOfWeek> dias);
}
