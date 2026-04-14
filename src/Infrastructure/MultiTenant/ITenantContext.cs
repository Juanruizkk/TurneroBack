namespace TurneroApp.Infrastructure.MultiTenant;

/// <summary>
/// Provee el SucursalId del tenant autenticado en el request actual.
/// Null cuando no hay SucursalId en el JWT (SuperAdmin, registro, endpoints públicos).
/// </summary>
public interface ITenantContext
{
    Guid? SucursalId { get; }
}
