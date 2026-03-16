using TurneroApp.Domain.Entities;

namespace TurneroApp.Application.Interfaces;

public interface ITurnoRepository : IGenericRepository<Turno>
{
    Task<Turno?> GetByIdConRelacionesAsync(Guid id);
    Task<IEnumerable<Turno>> GetBySucursalAsync(Guid sucursalId);
    Task<IEnumerable<Turno>> GetByProfesionalAsync(Guid profesionalId, DateTime? desde = null, DateTime? hasta = null);
    Task<IEnumerable<Turno>> GetByClienteAsync(Guid clienteId);
    Task<Turno?> GetByTokenAsync(string token);
    Task<bool> ExisteConflictoHorarioAsync(Guid profesionalId, DateTime inicio, DateTime fin, Guid? excludeTurnoId = null);

    /// <summary>Turnos activos de una sucursal entre dos fechas (para cálculo de slots).</summary>
    Task<IEnumerable<Turno>> GetActivosSucursalEnRangoAsync(Guid sucursalId, DateTime desde, DateTime hasta);

    /// <summary>
    /// Turnos en estado Confirmado o Pendiente cuya FechaHoraFin es anterior a <paramref name="fechaLimite"/>.
    /// Usado por el job de auto-NoShow.
    /// </summary>
    Task<IEnumerable<Turno>> GetConfirmadosPendientesAntesDeAsync(DateTime fechaLimite);

    /// <summary>
    /// Turnos que deben recibir un recordatorio por email:
    /// estado Confirmado o Pendiente, inicio futuro, y al menos un flag de recordatorio sin enviar
    /// dentro de las ventanas de <paramref name="limite24h"/> o <paramref name="limite3h"/>.
    /// Incluye Cliente, Servicio, Profesional y Sucursal para construir el email.
    /// </summary>
    Task<IEnumerable<Turno>> GetParaRecordatoriosAsync(DateTime ahora, DateTime limite24h, DateTime limite3h);
}
