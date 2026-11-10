using TurneroApp.Application.DTOs.Demo;

namespace TurneroApp.Application.Interfaces;

/// <summary>
/// Crea sucursales demo efímeras (clon aislado del dataset "Zen Massage Center")
/// y hace el teardown ordenado por SucursalId. Reutilizado por el controller y el
/// background service de limpieza.
/// </summary>
public interface IDemoTenantService
{
    /// <summary>
    /// Crea una sucursal demo efímera con emails únicos. Devuelve los datos de sesión
    /// SIN tokens (la capa Api mintea los JWT).
    /// Lanza <see cref="Exceptions.DemoLimiteException"/> si se exceden los límites.
    /// </summary>
    Task<DemoCreacionResultado> CrearAsync(string ip, CancellationToken ct = default);

    /// <summary>Borra en orden toda la data de una sucursal demo (idempotente).</summary>
    Task TeardownAsync(Guid sucursalId, CancellationToken ct = default);

    /// <summary>Hace teardown de todas las demos vencidas. Devuelve cuántas borró.</summary>
    Task<int> LimpiarVencidasAsync(CancellationToken ct = default);
}
