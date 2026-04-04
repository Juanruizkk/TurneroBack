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

    /// <summary>Devuelve el estado de completitud del onboarding para una sucursal.</summary>
    [HttpGet]
    public async Task<IActionResult> GetStatus([FromQuery] Guid sucursalId)
    {
        var status = await _service.GetStatusAsync(sucursalId);
        return Ok(status);
    }
}
