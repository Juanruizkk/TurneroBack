namespace TurneroApp.Application.Interfaces;

public interface IPagoTurnoService
{
    /// <summary>
    /// Crea una preferencia de pago en MP para el turno indicado.
    /// Solo válido si el turno está en estado PendientePago.
    /// Persiste el PreferenceId en el turno.
    /// </summary>
    /// <returns>La checkout URL lista para redirigir (sandbox o producción según el token de la sucursal).</returns>
    Task<string> GenerarLinkDePagoAsync(Guid turnoId);

    /// <summary>
    /// Procesa la notificación de un pago recibida desde el webhook de MP.
    /// Si el pago fue aprobado: confirma el turno y registra el cobro en Caja.
    /// Idempotente: si el turno ya está Confirmado, no hace nada.
    /// </summary>
    Task ProcesarNotificacionPagoAsync(long paymentId, Guid sucursalId);

    /// <summary>
    /// Confirma el pago usando el payment_id y turnoId provenientes de la redirección de MP.
    /// Idempotente: si el turno ya está Confirmado, no hace nada.
    /// </summary>
    Task ConfirmarPagoDesdeRedirectAsync(long paymentId, Guid turnoId);

    /// <summary>
    /// Re-verifica el estado del pago consultando MP por external_reference.
    /// Útil como fallback cuando el webhook y el redirect fallaron.
    /// </summary>
    /// <returns>true si se encontró y procesó un pago aprobado, false si no hay pago aprobado.</returns>
    Task<bool> VerificarPagoAsync(Guid turnoId);
}
