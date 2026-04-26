using System.Security.Claims;
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

    private IActionResult? ValidarAccesoSucursal(Guid sucursalId)
    {
        var esSuperAdmin = User.FindFirstValue("esSuperAdmin") == "true";
        if (esSuperAdmin) return null;

        var claimSucursalId = User.FindFirstValue("sucursalId");
        if (!Guid.TryParse(claimSucursalId, out var tokenSucursalId) || tokenSucursalId != sucursalId)
            return Forbid();

        return null;
    }

    /// <summary>Obtiene el estado actual de la suscripción de una sucursal.</summary>
    [HttpGet]
    public async Task<IActionResult> Obtener([FromQuery] Guid sucursalId)
    {
        var check = ValidarAccesoSucursal(sucursalId);
        if (check is not null) return check;

        var result = await _service.ObtenerAsync(sucursalId);
        return Ok(result);
    }
}
