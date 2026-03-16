using TurneroApp.Domain.Entities;

namespace TurneroApp.Application.Interfaces;

public interface IClienteRepository : IGenericRepository<Cliente>
{
    Task<IEnumerable<Cliente>> GetBySucursalAsync(Guid sucursalId);
    Task<Cliente?> BuscarPorTelefonoAsync(Guid sucursalId, string telefono);
    Task<Cliente?> BuscarPorEmailAsync(Guid sucursalId, string email);
    Task<IEnumerable<Cliente>> BuscarAsync(Guid sucursalId, string q, int max = 10);
}
