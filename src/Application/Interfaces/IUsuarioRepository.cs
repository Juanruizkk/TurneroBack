using TurneroApp.Domain.Entities;

namespace TurneroApp.Application.Interfaces;

public interface IUsuarioRepository : IGenericRepository<Usuario>
{
    /// <summary>
    /// Obtiene un usuario por id incluyendo su colección de permisos.
    /// </summary>
    Task<Usuario?> GetByIdConPermisosAsync(Guid id);

    /// <summary>
    /// Lista todos los usuarios de una sucursal, incluyendo sus permisos.
    /// Ordena por Apellido, Nombre.
    /// </summary>
    Task<IEnumerable<Usuario>> GetBySucursalAsync(Guid sucursalId);

    /// <summary>
    /// Busca un usuario por email dentro de una sucursal.
    /// Útil para validar unicidad antes de crear o actualizar.
    /// </summary>
    Task<Usuario?> GetByEmailAsync(Guid sucursalId, string email);

    /// <summary>
    /// Busca usuarios por email en todas las sucursales (para login).
    /// Incluye permisos. Si hay varios (owner multi-sucursal), devuelve todos ordenados:
    /// activos primero, luego owners.
    /// </summary>
    Task<IEnumerable<Usuario>> GetByEmailGlobalAsync(string email);

    /// <summary>Busca un usuario por su token de invitación.</summary>
    Task<Usuario?> GetByInvitacionTokenAsync(string token);
}
