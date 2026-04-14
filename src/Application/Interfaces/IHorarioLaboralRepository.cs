using TurneroApp.Domain.Entities;

namespace TurneroApp.Application.Interfaces;

public interface IHorarioLaboralRepository
{
    /// <summary>
    /// Obtiene los horarios de un usuario por su UsuarioId (join a través de UsuarioSucursalRol).
    /// Útil para validaciones en TurnoService.
    /// </summary>
    Task<List<HorarioLaboral>> GetByUsuarioIdAsync(Guid usuarioId);

    /// <summary>
    /// Obtiene horarios en batch para múltiples UsuarioIds.
    /// El diccionario resultante está indexado por UsuarioId.
    /// </summary>
    Task<Dictionary<Guid, List<HorarioLaboral>>> GetByUsuariosAsync(IEnumerable<Guid> usuarioIds);

    /// <summary>
    /// Elimina todos los horarios del UsuarioSucursalRol e inserta los nuevos.
    /// Llama SaveChangesAsync para confirmar.
    /// </summary>
    Task ReplaceAsync(Guid usuarioSucursalRolId, IEnumerable<HorarioLaboral> nuevos);

    Task<int> SaveChangesAsync();
}
