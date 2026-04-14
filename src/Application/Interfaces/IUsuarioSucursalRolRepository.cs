using TurneroApp.Domain.Entities;

namespace TurneroApp.Application.Interfaces;

public interface IUsuarioSucursalRolRepository
{
    /// <summary>Obtiene un rol por su Id, incluyendo Usuario, Sucursal y Permisos.</summary>
    Task<UsuarioSucursalRol?> GetByIdAsync(Guid id);

    /// <summary>Busca por token de invitación. Incluye Usuario y Sucursal.</summary>
    Task<UsuarioSucursalRol?> GetByInvitacionTokenAsync(string token);

    /// <summary>Lista todos los roles de una sucursal, incluyendo Usuario y Permisos.</summary>
    Task<IEnumerable<UsuarioSucursalRol>> GetBySucursalAsync(Guid sucursalId);

    /// <summary>Lista todos los roles activos de un usuario (todas sus sucursales).</summary>
    Task<IEnumerable<UsuarioSucursalRol>> GetByUsuarioAsync(Guid usuarioId);

    /// <summary>Busca el rol de un usuario en una sucursal específica.</summary>
    Task<UsuarioSucursalRol?> GetByUsuarioYSucursalAsync(Guid usuarioId, Guid sucursalId);

    Task AddAsync(UsuarioSucursalRol rol);
    void Update(UsuarioSucursalRol rol);
    Task SaveChangesAsync();

    /// <summary>Elimina físicamente la membresía y sus hijos (cascade).</summary>
    Task DeleteHardAsync(Guid id);

    /// <summary>Cuenta usuarios activos y pendientes de invitación en una sucursal (reservan cupo).</summary>
    Task<int> ContarActivosBySucursalAsync(Guid sucursalId);
}
