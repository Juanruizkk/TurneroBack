using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurneroApp.Application.Interfaces;

namespace TurneroApp.Api.Controllers;

[ApiController]
[Route("api/mercadopago/oauth")]
[Produces("application/json")]
public class MercadoPagoOAuthController : ControllerBase
{
    private readonly IMercadoPagoOAuthService _oauthService;
    private readonly IConfiguration _configuration;

    public MercadoPagoOAuthController(
        IMercadoPagoOAuthService oauthService,
        IConfiguration configuration)
    {
        _oauthService  = oauthService;
        _configuration = configuration;
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

        var url = _oauthService.GenerarUrlAutorizacion(sucursalId);
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

    if (string.IsNullOrWhiteSpace(state) || !Guid.TryParseExact(state, "N", out var sucursalId))
    {
        return BadRequest(new
        {
            mensaje = "State inválido o ausente",
            state,
            code,
            error,
            error_description
        });
    }

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
