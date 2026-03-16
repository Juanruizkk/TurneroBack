using TurneroApp.Domain.Entities;

namespace TurneroApp.Application.Interfaces;

public interface IAdjuntoTurnoRepository
{
    Task<List<AdjuntoTurno>> GetByTurnoIdAsync(Guid turnoId);
    Task<AdjuntoTurno?>      GetByIdAsync(Guid id);
    Task                     AddAsync(AdjuntoTurno adjunto);
    void                     Delete(AdjuntoTurno adjunto);
    Task                     SaveChangesAsync();
}
