using TurneroApp.Domain.Entities;

namespace TurneroApp.Application.Interfaces;

public interface IComentarioTurnoRepository
{
    Task<List<ComentarioTurno>> GetByTurnoIdAsync(Guid turnoId);
    Task<ComentarioTurno?>      GetByIdAsync(Guid id);
    Task                        AddAsync(ComentarioTurno comentario);
    void                        Delete(ComentarioTurno comentario);
    Task                        SaveChangesAsync();
}
