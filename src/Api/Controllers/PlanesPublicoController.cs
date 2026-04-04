using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurneroApp.Application.DTOs.SuperAdmin;
using TurneroApp.Application.Services;

namespace TurneroApp.Api.Controllers;

[ApiController]
[Route("api/planes")]
[AllowAnonymous]
[Produces("application/json")]
public class PlanesPublicoController : ControllerBase
{
    private readonly SuperAdminService _service;

    public PlanesPublicoController(SuperAdminService service)
        => _service = service;

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlanes()
    {
        var planes = await _service.ListarPlanesAsync();
        return Ok(planes.Where(p => p.EsActivo).OrderBy(p => p.OrdenDisplay));
    }
}
