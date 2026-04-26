using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurneroApp.Application.Services;

namespace TurneroApp.Api.Controllers;

[ApiController]
[Route("api/onboarding")]
[Authorize]
public class OnboardingController : ControllerBase
{
    private readonly OnboardingService _service;

    public OnboardingController(OnboardingService service)
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

    /// <summary>Devuelve el estado de completitud del onboarding para una sucursal.</summary>
    [HttpGet]
    public async Task<IActionResult> GetStatus([FromQuery] Guid sucursalId)
    {
        var check = ValidarAccesoSucursal(sucursalId);
        if (check is not null) return check;

        var status = await _service.GetStatusAsync(sucursalId);
        return Ok(status);
    }
}
