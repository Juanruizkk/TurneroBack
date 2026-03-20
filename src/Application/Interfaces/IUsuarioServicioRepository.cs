using TurneroApp.Domain.Entities;

namespace TurneroApp.Application.Interfaces;

public interface IUsuarioServicioRepository
{
    Task<List<UsuarioServicio>> GetByUsuarioIdAsync(Guid usuarioId);
    Task<Dictionary<Guid, List<UsuarioServicio>>> GetByUsuariosAsync(IEnumerable<Guid> usuarioIds);
    /// <summary>Elimina todos los servicios del usuario e inserta los nuevos. Llama SaveChangesAsync para confirmar.</summary>
    Task ReplaceAsync(Guid usuarioId, IEnumerable<Guid> servicioIds);
    Task<int> SaveChangesAsync();
}
