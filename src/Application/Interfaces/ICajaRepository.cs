using TurneroApp.Domain.Entities;

namespace TurneroApp.Application.Interfaces;

public interface ICajaRepository : IGenericRepository<Caja>
{
    Task<Caja?> GetBySucursalAsync(Guid sucursalId);
    Task<IEnumerable<MovimientoCaja>> GetMovimientosByCajaAsync(Guid cajaId, DateTime? desde = null, DateTime? hasta = null);
    Task AddMovimientoAsync(MovimientoCaja movimiento);
}
