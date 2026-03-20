using TurneroApp.Domain.Entities;

namespace TurneroApp.Application.Interfaces;

public interface IHorarioLaboralRepository
{
    Task<List<HorarioLaboral>> GetByUsuarioIdAsync(Guid usuarioId);
    Task<Dictionary<Guid, List<HorarioLaboral>>> GetByUsuariosAsync(IEnumerable<Guid> usuarioIds);
    /// <summary>Elimina todos los horarios del usuario e inserta los nuevos. Llama SaveChangesAsync para confirmar.</summary>
    Task ReplaceAsync(Guid usuarioId, IEnumerable<HorarioLaboral> nuevos);
    Task<int> SaveChangesAsync();
}
