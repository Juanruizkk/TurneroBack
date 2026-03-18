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
            var preferenceId = await _pagoService.GenerarLinkDePagoAsync(turnoId);
            return Ok(new IniciarPagoResponseDto { PreferenceId = preferenceId });
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
