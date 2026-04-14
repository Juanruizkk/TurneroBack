using Microsoft.AspNetCore.Mvc;
using TurneroApp.Application.DTOs.Pagos;
using TurneroApp.Application.Interfaces;

namespace TurneroApp.Api.Controllers;

[ApiController]
[Route("api/pagos")]
[Produces("application/json")]
public class PagosController : ControllerBase
{
    private readonly IPagoTurnoService _pagoService;

    public PagosController(IPagoTurnoService pagoService)
        => _pagoService = pagoService;

    /// <summary>
    /// Confirma el pago de un turno a partir de la redirección de MP post-checkout.
    /// MP incluye payment_id y external_reference (= turnoId) en la URL de retorno.
    /// </summary>
    [HttpPost("confirmar-redirect")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmarRedirect([FromQuery] long paymentId, [FromQuery] Guid turnoId)
    {
        try
        {
            await _pagoService.ConfirmarPagoDesdeRedirectAsync(paymentId, turnoId);
            return Ok(new { message = "Pago procesado." });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("no encontrado"))
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Re-verifica el estado de un pago consultando MP directamente por external_reference.
    /// Útil como fallback cuando webhook y redirect fallaron (ej: el usuario cerró la pestaña).
    /// </summary>
    [HttpPost("turnos/{turnoId:guid}/verificar-pago")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerificarPago(Guid turnoId)
    {
        try
        {
            var confirmado = await _pagoService.VerificarPagoAsync(turnoId);
            return Ok(new { confirmado });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("no encontrado"))
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Genera un link de pago de Mercado Pago para el turno indicado.
    /// El frontend usa el preferenceId para renderizar el botón de MP.
    /// No requiere autenticación (el cliente lo llama desde el booking público).
    /// </summary>
    [HttpPost("turnos/{turnoId:guid}/iniciar-pago")]
    [ProducesResponseType(typeof(IniciarPagoResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> IniciarPago(Guid turnoId)
    {
        try
        {
            var checkoutUrl = await _pagoService.GenerarLinkDePagoAsync(turnoId);
            return Ok(new IniciarPagoResponseDto { CheckoutUrl = checkoutUrl });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("no encontrado"))
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
