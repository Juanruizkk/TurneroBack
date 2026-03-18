namespace TurneroApp.Application.Interfaces;

public interface IPagoTurnoService
{
    /// <summary>
    /// Crea una preferencia de pago en MP para el turno indicado.
    /// Solo válido si el turno está en estado PendientePago.
    /// Persiste el PreferenceId en el turno.
    /// </summary>
    /// <returns>El PreferenceId de MP para que el frontend renderice el botón.</returns>
    Task<string> GenerarLinkDePagoAsync(Guid turnoId);

    /// <summary>
    /// Procesa la notificación de un pago recibida desde el webhook de MP.
    /// Si el pago fue aprobado: confirma el turno y registra el cobro en Caja.
    /// Idempotente: si el turno ya está Confirmado, no hace nada.
    /// </summary>
    Task ProcesarNotificacionPagoAsync(long paymentId);
}
