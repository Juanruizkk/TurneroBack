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

    // ── Sesiones de caja ──────────────────────────────────────────────────────

    /// <summary>Retorna la sesión activa de la caja, o null si no hay ninguna abierta.</summary>
    Task<CajaSesion?> GetSesionActivaByCajaAsync(Guid cajaId);

    /// <summary>Abre una nueva sesión. Lanza si ya hay una sesión activa.</summary>
    Task<CajaSesion> AbrirSesionAsync(CajaSesion sesion);

    /// <summary>Cierra la sesión, persiste MontoFinalEsperado, MontoFinalReal y Diferencia.</summary>
    Task<CajaSesion> CerrarSesionAsync(
        Guid    sesionId,
        decimal montoFinalReal,
        decimal montoFinalEsperado,
        Guid?   usuarioCierreId,
        string? observaciones);

    /// <summary>Historial de sesiones de una caja, ordenadas por FechaApertura descendente.</summary>
    Task<IEnumerable<CajaSesion>> GetSesionesByCajaAsync(Guid cajaId);
}
