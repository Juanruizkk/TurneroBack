using TurneroApp.Application.DTOs.Pagos;

namespace TurneroApp.Application.Interfaces;

public interface IMercadoPagoGateway
{
    /// <summary>
    /// Crea una preferencia de pago en MP y retorna su ID.
    /// El turnoId se guarda en ExternalReference para identificar el turno en el webhook.
    /// </summary>
    /// <returns>
    /// Tupla con el PreferenceId y la CheckoutUrl correcta para el entorno
    /// (sandbox si el token empieza con "TEST-", producción en caso contrario).
    /// </returns>
    Task<(string PreferenceId, string CheckoutUrl)> CrearPreferenciaAsync(
        string  turnoId,
        string  tituloServicio,
        decimal monto,
        string  frontendUrl,
        string  accessToken,
        string? notificationUrl);

    /// <summary>
    /// Consulta el estado real de un pago por su ID (usado en el webhook).
    /// </summary>
    Task<PaymentDetailsDto> ObtenerDetallesPagoAsync(long paymentId, string accessToken);

    /// <summary>
    /// Busca el pago aprobado más reciente para un external_reference dado.
    /// Retorna null si no existe ningún pago aprobado.
    /// </summary>
    Task<PaymentDetailsDto?> BuscarPagoPorReferenciaAsync(string externalReference, string accessToken);
}
