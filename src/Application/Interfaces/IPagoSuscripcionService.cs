using TurneroApp.Application.DTOs.Pagos;

namespace TurneroApp.Application.Interfaces;

public interface IPagoSuscripcionService
{
    /// <summary>
    /// Genera un link de Checkout Pro apuntado a la cuenta del sistema.
    /// </summary>
    Task<string> GenerarLinkDePagoAsync(Guid sucursalId, Guid planId);

    /// <summary>
    /// Procesa la notificación de MP y actualiza la suscripción si el pago fue aprobado.
    /// </summary>
    Task ProcesarWebhookAsync(long paymentId);

    /// <summary>
    /// Devuelve el historial de pagos de suscripción para la sucursal.
    /// </summary>
    Task<IEnumerable<PagoSuscripcionDto>> GetHistorialAsync(Guid sucursalId);
}
