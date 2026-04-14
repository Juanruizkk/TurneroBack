using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TurneroApp.Application.Interfaces;
using TurneroApp.Infrastructure.Services.MercadoPago;

namespace TurneroApp.Api.Controllers;

[ApiController]
[Route("api/webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly IPagoTurnoService           _pagoService;
    private readonly ILogger<WebhooksController> _logger;
    private readonly MercadoPagoOptions          _mpOptions;

    public WebhooksController(
        IPagoTurnoService           pagoService,
        ILogger<WebhooksController> logger,
        IOptions<MercadoPagoOptions> mpOptions)
    {
        _pagoService = pagoService;
        _logger      = logger;
        _mpOptions   = mpOptions.Value;
    }

    /// <summary>
    /// Endpoint que MP llama cuando el estado de un pago cambia.
    /// Siempre retorna 200 OK para que MP no reintente indefinidamente.
    /// </summary>
    [HttpPost("mercadopago/{sucursalId:guid}")]
    public async Task<IActionResult> MercadoPago(
        [FromRoute] Guid             sucursalId,
        [FromQuery] string?          type,
        [FromQuery] string?          topic,
        [FromBody]  MpWebhookPayload? payload)
    {
        var eventType = type ?? topic ?? payload?.Type;

        // Soporta tres formatos de notificación de Mercado Pago:
        // 1. Webhook v2 (body JSON):   {"type":"payment","data":{"id":"123"}}
        // 2. Webhook v2 (query param): ?data.id=123
        // 3. IPN legacy (query param): ?id=123&topic=payment
        var rawId = payload?.Data?.Id
                 ?? Request.Query["data.id"].FirstOrDefault()
                 ?? Request.Query["id"].FirstOrDefault();

        _logger.LogInformation("Webhook MP recibido: type={Type}, id={Id}, sucursalId={SucursalId}",
            eventType, rawId, sucursalId);

        // Seguridad: validar firma HMAC del webhook (solo si WebhookSecret está configurado)
        var xSignature  = Request.Headers["x-signature"].FirstOrDefault();
        var xRequestId  = Request.Headers["x-request-id"].FirstOrDefault();
        if (!ValidarFirmaWebhook(xSignature, xRequestId, rawId))
        {
            _logger.LogWarning(
                "Firma de webhook inválida. PaymentId={RawId}, x-signature={Signature}",
                rawId, xSignature);
            return Ok(); // respondemos 200 de todas formas para no revelar el rechazo a posibles atacantes
        }

        if (eventType != "payment" || string.IsNullOrEmpty(rawId))
            return Ok();

        if (!long.TryParse(rawId, out var paymentId))
        {
            _logger.LogWarning("ID de pago inválido: '{RawId}'", rawId);
            return Ok();
        }

        if (sucursalId == Guid.Empty)
        {
            _logger.LogWarning("Webhook sin sucursalId para PaymentId={PaymentId}. Ignorado.", paymentId);
            return Ok();
        }

        try
        {
            await _pagoService.ProcesarNotificacionPagoAsync(paymentId, sucursalId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando notificación MP para PaymentId={PaymentId}.", paymentId);
        }

        return Ok();
    }

    /// <summary>
    /// Valida la firma HMAC-SHA256 del webhook de Mercado Pago.
    /// Si <see cref="MercadoPagoOptions.WebhookSecret"/> no está configurado, el método devuelve true
    /// para no bloquear entornos de desarrollo sin secreto configurado.
    /// </summary>
    /// <remarks>
    /// Formato del header x-signature: <c>ts=&lt;unix_timestamp&gt;,v1=&lt;hmac_hex&gt;</c>
    /// Contenido firmado: <c>id:&lt;dataId&gt;;request-id:&lt;xRequestId&gt;;ts:&lt;ts&gt;</c>
    /// </remarks>
    private bool ValidarFirmaWebhook(string? xSignature, string? xRequestId, string? dataId)
    {
        if (string.IsNullOrEmpty(_mpOptions.WebhookSecret))
            return true; // secreto no configurado → se omite la validación (entorno de desarrollo)

        if (string.IsNullOrEmpty(xSignature))
        {
            _logger.LogWarning("Webhook MP sin header x-signature.");
            return false;
        }

        // Parsear ts y v1 del header
        string? ts = null, v1 = null;
        foreach (var part in xSignature.Split(','))
        {
            var kv = part.Trim().Split('=', 2);
            if (kv.Length == 2)
            {
                if (kv[0] == "ts") ts = kv[1];
                else if (kv[0] == "v1") v1 = kv[1];
            }
        }

        if (ts is null || v1 is null)
        {
            _logger.LogWarning("Header x-signature con formato inválido: '{Signature}'", xSignature);
            return false;
        }

        var manifest = $"id:{dataId};request-id:{xRequestId};ts:{ts}";
        var key      = Encoding.UTF8.GetBytes(_mpOptions.WebhookSecret);
        var data     = Encoding.UTF8.GetBytes(manifest);

        using var hmac     = new HMACSHA256(key);
        var       computed = Convert.ToHexString(hmac.ComputeHash(data)).ToLowerInvariant();

        var computedBytes = Encoding.UTF8.GetBytes(computed);
        var v1Bytes       = Encoding.UTF8.GetBytes(v1);
        return CryptographicOperations.FixedTimeEquals(computedBytes, v1Bytes);
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
