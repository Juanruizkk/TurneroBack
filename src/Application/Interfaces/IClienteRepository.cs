using TurneroApp.Application.DTOs.Cliente;
using TurneroApp.Domain.Entities;

namespace TurneroApp.Application.Interfaces;

public interface IClienteRepository : IGenericRepository<Cliente>
{
    Task<IEnumerable<Cliente>> GetBySucursalAsync(Guid sucursalId);
    Task<Cliente?> BuscarPorTelefonoAsync(Guid sucursalId, string telefono);
    Task<Cliente?> BuscarPorEmailAsync(Guid sucursalId, string email);
    Task<IEnumerable<Cliente>> BuscarAsync(Guid sucursalId, string q, int max = 10);

    Task<Cliente?> GetByIdIgnorandoFiltrosAsync(Guid id);
    Task<IEnumerable<Cliente>> GetArchivedBySucursalAsync(Guid sucursalId);
    Task<bool> TelefonoExisteEnSucursalAsync(Guid sucursalId, string telefono, Guid? excludeId = null);
    Task<ClienteFichaDto?> GetFichaAsync(Guid clienteId);
    Task<bool> TieneImpactoOperacionalAsync(Guid clienteId);
    Task EliminarTurnosNoCriticosAsync(Guid clienteId);

    /// <summary>
    /// Elimina los clientes de la lista que no tienen historial operativo ni pagos.
    /// Borra primero sus turnos no críticos (cascada limpia comentarios/adjuntos) y luego el cliente.
    /// Retorna la cantidad de clientes eliminados.
    /// </summary>
    Task<int> PurgarClientesFantasmaAsync(IEnumerable<Guid> clienteIds);
}
