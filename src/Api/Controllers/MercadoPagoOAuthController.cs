using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using TurneroApp.Application.Interfaces;
using TurneroApp.Infrastructure.Services.MercadoPago;

namespace TurneroApp.Api.Controllers;

[ApiController]
[Route("api/mercadopago/oauth")]
[Produces("application/json")]
public class MercadoPagoOAuthController : ControllerBase
{
    private readonly IMercadoPagoOAuthService _oauthService;
    private readonly IConfiguration _configuration;
    private readonly MercadoPagoOAuthOptions _oauthOptions;

    public MercadoPagoOAuthController(
        IMercadoPagoOAuthService oauthService,
        IConfiguration configuration,
        IOptions<MercadoPagoOAuthOptions> oauthOptions)
    {
        _oauthService  = oauthService;
        _configuration = configuration;
        _oauthOptions  = oauthOptions.Value;
    }

    /// <summary>Genera un state HMAC-firmado para el flujo OAuth.</summary>
    private string GenerarStateFirmado(Guid sucursalId)
    {
        var secret = _oauthOptions.StateSecret;
        if (string.IsNullOrEmpty(secret))
            throw new InvalidOperationException("MercadoPagoOAuth:StateSecret no configurado.");

        var payload = $"{sucursalId:N}:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var firma = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{payload}.{firma}"));
    }

    /// <summary>
    /// Verifica el state HMAC. Devuelve el sucursalId si la firma es válida, o null si no.
    /// </summary>
    private Guid? VerificarState(string state)
    {
        try
        {
            var secret = _oauthOptions.StateSecret;
            if (string.IsNullOrEmpty(secret)) return null;

            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(state));
            var dotPos = decoded.LastIndexOf('.');
            if (dotPos < 0) return null;

            var payload = decoded[..dotPos];
            var firmaRecibida = decoded[(dotPos + 1)..];

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var firmaEsperada = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));

            if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(firmaEsperada),
                Encoding.UTF8.GetBytes(firmaRecibida)))
                return null;

            var partes = payload.Split(':');
            return Guid.TryParseExact(partes[0], "N", out var id) ? id : null;
        }
        catch { return null; }
    }

    /// <summary>
    /// Inicia el flujo OAuth. Redirige al usuario a Mercado Pago.
    /// El frontend llama a este endpoint con el sucursalId de la sucursal a conectar.
    /// Requiere autenticación (solo owners/admins conectan su cuenta).
    /// </summary>
    [HttpGet("start")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult Start([FromQuery] Guid sucursalId)
    {
        if (sucursalId == Guid.Empty)
            return BadRequest("sucursalId es requerido.");

        string state;
        try { state = GenerarStateFirmado(sucursalId); }
        catch (InvalidOperationException ex) { return StatusCode(500, new { message = ex.Message }); }

        // Construir la URL directamente usando el state firmado
        var query = new Dictionary<string, string?>
        {
            ["client_id"]     = _oauthOptions.ClientId,
            ["response_type"] = "code",
            ["platform_id"]   = "mp",
            ["state"]         = state,
            ["redirect_uri"]  = _oauthOptions.RedirectUri,
        };
        var url = QueryHelpers.AddQueryString(_oauthOptions.AuthorizationBaseUrl, query!);
        return Ok(new { url });
    }

    /// <summary>
    /// Devuelve si la sucursal ya tiene una cuenta de Mercado Pago conectada y activa.
    /// </summary>
    [HttpGet("estado")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> Estado([FromQuery] Guid sucursalId)
    {
        var estado = await _oauthService.ObtenerEstadoAsync(sucursalId);
        return Ok(estado);
    }

    /// <summary>
    /// Callback que Mercado Pago llama después de que el usuario autorizó.
    /// Intercambia el code por tokens y los persiste en la DB.
    /// NO requiere JWT (Mercado Pago llama a este endpoint directamente).
    /// </summary>
   [HttpGet("callback")]
[AllowAnonymous]
[ProducesResponseType(StatusCodes.Status302Found)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<IActionResult> Callback(
    [FromQuery] string? code,
    [FromQuery] string? state,
    [FromQuery] string? error,
    [FromQuery] string? error_description)
{
    if (!string.IsNullOrWhiteSpace(error))
    {
        return BadRequest(new
        {
            mensaje = "Mercado Pago devolvió un error",
            error,
            error_description,
            state
        });
    }

    if (string.IsNullOrWhiteSpace(code))
    {
        return BadRequest(new
        {
            mensaje = "Mercado Pago no devolvió el code de autorización",
            code,
            state,
            error,
            error_description
        });
    }

    if (string.IsNullOrWhiteSpace(state))
    {
        return BadRequest(new { mensaje = "State ausente en el callback de Mercado Pago." });
    }

    var sucursalIdVerificado = VerificarState(state);
    if (sucursalIdVerificado is null)
    {
        return BadRequest(new { mensaje = "State inválido o firma HMAC incorrecta. Posible ataque CSRF." });
    }

    var sucursalId = sucursalIdVerificado.Value;

    try
    {
        await _oauthService.ConectarAsync(sucursalId, code);
    }
    catch (InvalidOperationException ex)
    {
        return BadRequest(new
        {
            mensaje = "Error al conectar con Mercado Pago",
            detalle = ex.Message,
            code,
            state
        });
    }

    var frontendUrl = _configuration["App:FrontendUrl"] ?? "http://localhost:5173";
    return Redirect($"{frontendUrl}/admin/ajustes?mp=conectado");
}

    /// <summary>
    /// Desvincula la cuenta de Mercado Pago de una sucursal.
    /// </summary>
    [HttpDelete("desconectar")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Desconectar([FromQuery] Guid sucursalId)
    {
        try
        {
            await _oauthService.DesconectarAsync(sucursalId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Fuerza el refresh del access_token de una sucursal.
    /// Útil para pruebas o en caso de token comprometido.
    /// </summary>
    [HttpPost("{sucursalId:guid}/refresh")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Refresh(Guid sucursalId)
    {
        try
        {
            await _oauthService.RefrescarTokenAsync(sucursalId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }
}
