using MercadoPago.Client;
using MercadoPago.Client.Payment;
using MercadoPago.Client.Preference;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TurneroApp.Application.DTOs.Pagos;
using TurneroApp.Application.Interfaces;

namespace TurneroApp.Infrastructure.Services.MercadoPago;

public sealed class MercadoPagoGateway : IMercadoPagoGateway
{
    private readonly ILogger<MercadoPagoGateway> _logger;
    private readonly MercadoPagoOptions          _options;

    public MercadoPagoGateway(
        ILogger<MercadoPagoGateway> logger,
        IOptions<MercadoPagoOptions> options)
    {
        _logger  = logger;
        _options = options.Value;
    }
    /// <inheritdoc/>
    public async Task<(string PreferenceId, string CheckoutUrl)> CrearPreferenciaAsync(
        string turnoId,
        string tituloServicio,
        decimal monto,
        string frontendUrl,
        string accessToken,
        string? notificationUrl)
    {
        var requestOptions = new RequestOptions { AccessToken = accessToken };

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

            NotificationUrl = notificationUrl,

            BackUrls = new PreferenceBackUrlsRequest
            {
                Success = $"{frontendUrl}/reserva/pago/exitoso",
                Failure = $"{frontendUrl}/reserva/pago/fallido",
                Pending = $"{frontendUrl}/reserva/pago/pendiente",
            },
            AutoReturn = frontendUrl.Contains("localhost") ? null : "approved",
        };

        var client = new PreferenceClient();
        var response = await client.CreateAsync(preference, requestOptions);

        // Los tokens de prueba empiezan con "TEST-"; los productivos con "APP_USR-".
        var checkoutUrl = response.SandboxInitPoint ?? response.InitPoint;

        _logger.LogInformation(
            "Preferencia creada: Id={Id} | NotificationUrl={NotificationUrl} | TokenPrefix={Prefix} | InitPoint={Init} | SandboxInitPoint={Sandbox} | CheckoutUrl={Checkout}",
            response.Id,
            notificationUrl,
            accessToken[..Math.Min(12, accessToken.Length)],
            response.InitPoint,
            response.SandboxInitPoint,
            checkoutUrl);

        return (response.Id, checkoutUrl);
    }

    /// <inheritdoc/>
    public async Task<PaymentDetailsDto> ObtenerDetallesPagoAsync(long paymentId, string accessToken)
    {
        var requestOptions = new RequestOptions { AccessToken = accessToken };
        var client = new PaymentClient();
        var payment = await client.GetAsync(paymentId, requestOptions);

        return new PaymentDetailsDto
        {
            PaymentId = payment.Id ?? 0,
            Status = payment.Status ?? string.Empty,
            StatusDetail = payment.StatusDetail ?? string.Empty,
            ExternalReference = payment.ExternalReference ?? string.Empty,
            Amount = payment.TransactionAmount ?? 0m,
        };
    }

    /// <inheritdoc/>
    public async Task<PaymentDetailsDto?> BuscarPagoPorReferenciaAsync(string externalReference, string accessToken)
    {
        var requestOptions = new RequestOptions { AccessToken = accessToken };
        var client = new PaymentClient();

        var searchRequest = new SearchRequest
        {
            Filters = new Dictionary<string, object>
            {
                ["external_reference"] = externalReference,
                ["sort"] = "date_created",
                ["criteria"] = "desc",
            }
        };

        var results = await client.SearchAsync(searchRequest, requestOptions);
        var approved = results.Results?.FirstOrDefault(p => p.Status == "approved");

        if (approved is null)
            return null;

        return new PaymentDetailsDto
        {
            PaymentId = approved.Id ?? 0,
            Status = approved.Status ?? string.Empty,
            StatusDetail = approved.StatusDetail ?? string.Empty,
            ExternalReference = approved.ExternalReference ?? string.Empty,
            Amount = approved.TransactionAmount ?? 0m,
        };
    }
}
