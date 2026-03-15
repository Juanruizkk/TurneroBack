using Microsoft.AspNetCore.Mvc;
using TurneroApp.Application.DTOs.Caja;
using TurneroApp.Application.Services;

namespace TurneroApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CajaController : ControllerBase
{
    private readonly CajaService _cajaService;

    public CajaController(CajaService cajaService)
    {
        _cajaService = cajaService;
    }

    /// <summary>
    /// Obtiene la caja asociada a una sucursal.
    /// </summary>
    [HttpGet("sucursal/{sucursalId:guid}")]
    [ProducesResponseType(typeof(CajaResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBySucursal(Guid sucursalId)
    {
        var caja = await _cajaService.GetBySucursalAsync(sucursalId);
        if (caja is null) return NotFound(new { message = $"No se encontró una caja para la sucursal {sucursalId}." });
        return Ok(caja);
    }

    /// <summary>
    /// Obtiene los movimientos de una caja, con filtros opcionales de fecha.
    /// </summary>
    [HttpGet("{cajaId:guid}/movimientos")]
    [ProducesResponseType(typeof(IEnumerable<MovimientoCajaResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMovimientos(
        Guid cajaId,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta)
    {
        var movimientos = await _cajaService.GetMovimientosAsync(cajaId, desde, hasta);
        return Ok(movimientos);
    }

    /// <summary>
    /// Registra un nuevo movimiento en la caja.
    /// </summary>
    [HttpPost("{cajaId:guid}/movimientos")]
    [ProducesResponseType(typeof(MovimientoCajaResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegistrarMovimiento(
        Guid cajaId,
        [FromBody] CreateMovimientoCajaDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (dto.CajaId != cajaId)
            return BadRequest(new { message = "El CajaId del body debe coincidir con el de la URL." });

        try
        {
            var movimiento = await _cajaService.RegistrarMovimientoAsync(dto);
            return CreatedAtAction(nameof(GetMovimientos), new { cajaId = movimiento.CajaId }, movimiento);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
