using TurneroApp.Domain.Entities;

namespace TurneroApp.Application.Interfaces;

public interface ICajaRepository : IGenericRepository<Caja>
{
    Task<Caja?> GetBySucursalAsync(Guid sucursalId);

    Task<IEnumerable<MovimientoCaja>> GetMovimientosByCajaAsync(
        Guid cajaId, DateTime? desde = null, DateTime? hasta = null);

    Task AddMovimientoAsync(MovimientoCaja movimiento);

    /// <summary>
    /// Inserta el movimiento, el pago de turno (si aplica) y actualiza TotalCobrado
    /// del turno en una sola transacción atómica.
    /// </summary>
    Task<MovimientoCaja> RegistrarMovimientoTransaccionalAsync(
        MovimientoCaja movimiento,
        PagoTurno?     pagoTurno,
        Turno?         turnoAActualizar);

    /// <summary>
    /// Todos los movimientos de una caja dentro del rango [desde, hasta] (inclusive).
    /// </summary>
    Task<IEnumerable<MovimientoCaja>> GetMovimientosRangoAsync(
        Guid cajaId, DateTime desde, DateTime hasta);

    /// <summary>
    /// Solo los movimientos de tipo CobroTurno, incluyendo el Turno y su Profesional,
    /// para armar el resumen de liquidaciones.
    /// </summary>
    Task<IEnumerable<MovimientoCaja>> GetCobrosTurnoConProfesionalAsync(
        Guid cajaId, DateTime desde, DateTime hasta);
}
