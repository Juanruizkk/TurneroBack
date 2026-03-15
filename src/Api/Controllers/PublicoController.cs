using Microsoft.AspNetCore.Mvc;
using TurneroApp.Application.Services;

namespace TurneroApp.Api.Controllers;

/// <summary>Endpoints públicos (sin autenticación) para el booking online.</summary>
[ApiController]
[Route("api/publico")]
public class PublicoController : ControllerBase
{
    private readonly PublicoService _service;

    public PublicoController(PublicoService service)
    {
        _service = service;
    }

    /// <summary>Perfil público de la sucursal (nombre, slogan, foto, rubros, horarios).</summary>
    [HttpGet("perfil")]
    public async Task<IActionResult> Perfil([FromQuery] Guid sucursalId)
    {
        try
        {
            var perfil = await _service.ObtenerPerfilAsync(sucursalId);
            return Ok(perfil);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Slots horarios disponibles para una fecha y servicio dados.
    /// Devuelve lista de { hora, profesionalId } para usar al crear el turno.
    /// </summary>
    [HttpGet("slots")]
    public async Task<IActionResult> Slots(
        [FromQuery] Guid sucursalId,
        [FromQuery] string fecha,       // "yyyy-MM-dd"
        [FromQuery] Guid servicioId)
    {
        if (!DateOnly.TryParse(fecha, out var dateOnly))
            return BadRequest(new { message = "Formato de fecha inválido. Use yyyy-MM-dd." });

        try
        {
            var slots = await _service.ObtenerSlotsAsync(sucursalId, dateOnly, servicioId);
            return Ok(slots);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message, detail = ex.InnerException?.Message });
        }
    }
}
