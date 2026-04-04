using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurneroApp.Application.Services;

namespace TurneroApp.Api.Controllers;

[ApiController]
[Route("api/suscripcion")]
[Authorize]
public class SuscripcionController : ControllerBase
{
    private readonly SuscripcionService _service;

    public SuscripcionController(SuscripcionService service)
    {
        _service = service;
    }

    /// <summary>Obtiene el estado actual de la suscripción de una sucursal.</summary>
    [HttpGet]
    public async Task<IActionResult> Obtener([FromQuery] Guid sucursalId)
    {
        var result = await _service.ObtenerAsync(sucursalId);
        return Ok(result);
    }
}
