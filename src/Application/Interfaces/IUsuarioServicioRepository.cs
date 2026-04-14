using TurneroApp.Domain.Entities;

namespace TurneroApp.Application.Interfaces;

public interface IUsuarioServicioRepository
{
    /// <summary>
    /// Obtiene los servicios de un usuario por su UsuarioId (join a través de UsuarioSucursalRol).
    /// Útil para validaciones en TurnoService.
    /// </summary>
    Task<List<UsuarioServicio>> GetByUsuarioIdAsync(Guid usuarioId);

    /// <summary>
    /// Obtiene servicios en batch para múltiples UsuarioIds.
    /// El diccionario resultante está indexado por UsuarioId.
    /// </summary>
    Task<Dictionary<Guid, List<UsuarioServicio>>> GetByUsuariosAsync(IEnumerable<Guid> usuarioIds);

    /// <summary>
    /// Elimina todos los servicios del UsuarioSucursalRol e inserta los nuevos.
    /// Llama SaveChangesAsync para confirmar.
    /// </summary>
    Task ReplaceAsync(Guid usuarioSucursalRolId, IEnumerable<Guid> servicioIds);

    Task<int> SaveChangesAsync();
}
