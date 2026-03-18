using TurneroApp.Application.DTOs.Pagos;

namespace TurneroApp.Application.Interfaces;

public interface IMercadoPagoGateway
{
    /// <summary>
    /// Crea una preferencia de pago en MP y retorna su ID.
    /// El turnoId se guarda en ExternalReference para identificar el turno en el webhook.
    /// </summary>
    Task<string> CrearPreferenciaAsync(
        string  turnoId,
        string  tituloServicio,
        decimal monto,
        string  frontendUrl);

    /// <summary>
    /// Consulta el estado real de un pago por su ID (usado en el webhook).
    /// </summary>
    Task<PaymentDetailsDto> ObtenerDetallesPagoAsync(long paymentId);
}
