using MercadoPago.Client;
using MercadoPago.Client.Payment;
using MercadoPago.Client.Preference;
using MercadoPago.Config;
using Microsoft.Extensions.Options;
using TurneroApp.Application.DTOs.Pagos;
using TurneroApp.Application.Interfaces;

namespace TurneroApp.Infrastructure.Services.MercadoPago;

public sealed class MercadoPagoGateway : IMercadoPagoGateway
{
    private readonly MercadoPagoOptions _options;

    public MercadoPagoGateway(IOptions<MercadoPagoOptions> options)
    {
        _options = options.Value;
        MercadoPagoConfig.AccessToken = _options.AccessToken;
    }

    /// <inheritdoc/>
    public async Task<string> CrearPreferenciaAsync(
        string  turnoId,
        string  tituloServicio,
        decimal monto,
        string  frontendUrl)
    {
        var requestOptions = new RequestOptions { AccessToken = _options.AccessToken };

        var preference = new PreferenceRequest
        {
            ExternalReference = turnoId,

            Items =
            [
                new PreferenceItemRequest
                {
                    Title      = tituloServicio,
                    Quantity   = 1,
                    CurrencyId = "ARS",
                    UnitPrice  = monto,
                }
            ],

            NotificationUrl = string.IsNullOrEmpty(_options.NotificationUrl) ? null : _options.NotificationUrl,

            // AutoReturn requiere back_urls públicas (no localhost).
            // Solo se activa si la URL del frontend no es localhost.
            BackUrls = frontendUrl.Contains("localhost") ? null : new PreferenceBackUrlsRequest
            {
                Success = $"{frontendUrl}/reserva/pago/exitoso",
                Failure = $"{frontendUrl}/reserva/pago/fallido",
                Pending = $"{frontendUrl}/reserva/pago/pendiente",
            },
            AutoReturn = frontendUrl.Contains("localhost") ? null : "approved",
        };

        var client   = new PreferenceClient();
        var response = await client.CreateAsync(preference, requestOptions);

        return response.Id;
    }

    /// <inheritdoc/>
    public async Task<PaymentDetailsDto> ObtenerDetallesPagoAsync(long paymentId)
    {
        var requestOptions = new RequestOptions { AccessToken = _options.AccessToken };
        var client  = new PaymentClient();
        var payment = await client.GetAsync(paymentId, requestOptions);

        return new PaymentDetailsDto
        {
            PaymentId         = payment.Id ?? 0,
            Status            = payment.Status            ?? string.Empty,
            StatusDetail      = payment.StatusDetail      ?? string.Empty,
            ExternalReference = payment.ExternalReference ?? string.Empty,
            Amount            = payment.TransactionAmount ?? 0m,
        };
    }
}
