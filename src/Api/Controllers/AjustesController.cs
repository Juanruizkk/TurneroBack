using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurneroApp.Application.DTOs.Ajustes;
using TurneroApp.Application.Services;

namespace TurneroApp.Api.Controllers;

[ApiController]
[Route("api/ajustes")]
[Authorize]
public class AjustesController : ControllerBase
{
    private readonly AjustesService _service;

    public AjustesController(AjustesService service)
    {
        _service = service;
    }

    /// <summary>Obtiene los ajustes completos de una sucursal.</summary>
    [HttpGet]
    public async Task<IActionResult> Obtener([FromQuery] Guid sucursalId)
    {
        try
        {
            var result = await _service.ObtenerAsync(sucursalId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Actualiza los ajustes de una sucursal.</summary>
    [HttpPut]
    public async Task<IActionResult> Actualizar([FromQuery] Guid sucursalId, [FromBody] ActualizarAjustesDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _service.ActualizarAsync(sucursalId, dto);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
