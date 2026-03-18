using Microsoft.AspNetCore.Mvc;
using TurneroApp.Application.Interfaces;

namespace TurneroApp.Api.Controllers;

[ApiController]
[Route("api/webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly IPagoTurnoService          _pagoService;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(
        IPagoTurnoService           pagoService,
        ILogger<WebhooksController> logger)
    {
        _pagoService = pagoService;
        _logger      = logger;
    }

    /// <summary>
    /// Endpoint que MP llama cuando el estado de un pago cambia.
    /// Siempre retorna 200 OK para que MP no reintente indefinidamente.
    /// </summary>
    [HttpPost("mercadopago")]
    public async Task<IActionResult> MercadoPago(
        [FromQuery] string?          type,
        [FromQuery] string?          topic,
        [FromBody]  MpWebhookPayload? payload)
    {
        var eventType = type ?? topic ?? payload?.Type;
        var rawId     = Request.Query["data.id"].FirstOrDefault()
                     ?? payload?.Data?.Id;

        _logger.LogInformation("Webhook MP recibido: type={Type}, id={Id}", eventType, rawId);

        if (eventType != "payment" || string.IsNullOrEmpty(rawId))
            return Ok();

        if (!long.TryParse(rawId, out var paymentId))
        {
            _logger.LogWarning("ID de pago inválido: '{RawId}'", rawId);
            return Ok();
        }

        try
        {
            await _pagoService.ProcesarNotificacionPagoAsync(paymentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando notificación MP para PaymentId={PaymentId}.", paymentId);
        }

        return Ok();
    }
}

public sealed class MpWebhookPayload
{
    public string?       Type { get; set; }
    public MpWebhookData? Data { get; set; }
}

public sealed class MpWebhookData
{
    public string? Id { get; set; }
}
