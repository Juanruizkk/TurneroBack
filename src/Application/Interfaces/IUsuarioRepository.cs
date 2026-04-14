using TurneroApp.Domain.Entities;

namespace TurneroApp.Application.Interfaces;

public interface IUsuarioRepository : IGenericRepository<Usuario>
{
    /// <summary>Busca un usuario por email (global — un único registro por email).</summary>
    Task<Usuario?> GetByEmailAsync(string email);

    /// <summary>
    /// Igual que GetByEmailAsync. Mantenido por compatibilidad con flujos existentes.
    /// </summary>
    Task<Usuario?> GetByEmailGlobalAsync(string email);

    /// <summary>Busca un usuario por su token de recuperación de contraseña.</summary>
    Task<Usuario?> GetByTokenRecuperacionAsync(string token);

    /// <summary>Busca el usuario que tenga un SeleccionToken activo.</summary>
    Task<Usuario?> GetBySeleccionTokenAsync(string token);
}
