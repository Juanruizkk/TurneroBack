using TurneroApp.Domain.Entities;

namespace TurneroApp.Application.Interfaces;

public interface INotificacionRepository
{
    Task AddAsync(Notificacion notificacion);

    /// <summary>Devuelve hasta 50 notificaciones no leídas de la sucursal, más recientes primero.</summary>
    Task<List<Notificacion>> GetNoLeidasBySucursalAsync(Guid sucursalId);

    /// <summary>Marca todas las notificaciones no leídas de la sucursal como leídas.</summary>
    Task MarcarTodasLeidasAsync(Guid sucursalId);

    /// <summary>Elimina todas las notificaciones de la sucursal (acción "Limpiar todo").</summary>
    Task EliminarBySucursalAsync(Guid sucursalId);

    Task<int> SaveChangesAsync();
}
