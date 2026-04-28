using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurneroApp.Application.DTOs.SuperAdmin;
using TurneroApp.Application.Services;

namespace TurneroApp.Api.Controllers;

[ApiController]
[Route("api/superadmin")]
[Produces("application/json")]
[Authorize(Roles = "SuperAdmin")]
public class SuperAdminController : ControllerBase
{
    private readonly SuperAdminService _service;

    public SuperAdminController(SuperAdminService service)
    {
        _service = service;
    }

    // GET /api/superadmin/sucursales
    [HttpGet("sucursales")]
    [ProducesResponseType(typeof(IEnumerable<SucursalSuperAdminDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar()
    {
        var list = await _service.ListarAsync();
        return Ok(list);
    }

    // GET /api/superadmin/sucursales/{id}
    [HttpGet("sucursales/{id:guid}")]
    [ProducesResponseType(typeof(SucursalSuperAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id)
    {
        var dto = await _service.ObtenerAsync(id);
        if (dto is null) return NotFound(new { message = $"Sucursal con Id {id} no encontrada." });
        return Ok(dto);
    }

    // POST /api/superadmin/sucursales
    [HttpPost("sucursales")]
    [ProducesResponseType(typeof(SucursalSuperAdminDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Crear([FromBody] CrearSucursalConOwnerDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var result = await _service.CrearConOwnerAsync(dto);
        return CreatedAtAction(nameof(Obtener), new { id = result.Id }, result);
    }

    // PUT /api/superadmin/sucursales/{id}
    [HttpPut("sucursales/{id:guid}")]
    [ProducesResponseType(typeof(SucursalSuperAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] ActualizarSucursalDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var result = await _service.ActualizarAsync(id, dto);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // POST /api/superadmin/usuarios/sincronizar-password
    /// <summary>
    /// Establece el mismo password en todos los registros activos de un email.
    /// Úsalo cuando un usuario trabaja en varias sucursales y sus passwords no coinciden.
    /// </summary>
    [HttpPost("usuarios/sincronizar-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SincronizarPassword([FromBody] SincronizarPasswordDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var cantidad = await _service.SincronizarPasswordAsync(dto.Email, dto.NuevoPassword);
            return Ok(new { message = $"Password actualizado en {cantidad} sucursal(es) para {dto.Email}." });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // PATCH /api/superadmin/sucursales/{id}/estado
    [HttpPatch("sucursales/{id:guid}/estado")]
    [ProducesResponseType(typeof(SucursalSuperAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleEstado(Guid id)
    {
        try
        {
            var result = await _service.ToggleActivaAsync(id);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // ─── Planes ───────────────────────────────────────────────────────────────

    // GET /api/superadmin/planes
    [HttpGet("planes")]
    [ProducesResponseType(typeof(IEnumerable<PlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarPlanes()
    {
        var list = await _service.ListarPlanesAsync();
        return Ok(list);
    }

    // GET /api/superadmin/planes/{id}
    [HttpGet("planes/{id:guid}", Name = nameof(ObtenerPlan))]
    [ProducesResponseType(typeof(PlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerPlan(Guid id)
    {
        var dto = await _service.ObtenerPlanAsync(id);
        if (dto is null) return NotFound(new { message = "Plan no encontrado." });
        return Ok(dto);
    }

    // POST /api/superadmin/planes
    [HttpPost("planes")]
    [ProducesResponseType(typeof(PlanDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearPlan([FromBody] CrearPlanDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await _service.CrearPlanAsync(dto);
        return CreatedAtAction(nameof(ObtenerPlan), new { id = result.Id }, result);
    }

    // PUT /api/superadmin/planes/{id}
    [HttpPut("planes/{id:guid}")]
    [ProducesResponseType(typeof(PlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActualizarPlan(Guid id, [FromBody] ActualizarPlanDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var result = await _service.ActualizarPlanAsync(id, dto);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // PATCH /api/superadmin/planes/{id}/precio
    [HttpPatch("planes/{id:guid}/precio")]
    [ProducesResponseType(typeof(PlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CambiarPrecioPlan(Guid id, [FromBody] CambiarPrecioPlanDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var result = await _service.CambiarPrecioPlanAsync(id, dto);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // PATCH /api/superadmin/planes/{id}/estado
    [HttpPatch("planes/{id:guid}/estado")]
    [ProducesResponseType(typeof(PlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleActivoPlan(Guid id)
    {
        try
        {
            var result = await _service.ToggleActivoPlanAsync(id);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // ─── Suscripciones ────────────────────────────────────────────────────────

    // GET /api/superadmin/suscripciones
    [HttpGet("suscripciones")]
    [ProducesResponseType(typeof(IEnumerable<SuscripcionSuperAdminDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarSuscripciones()
    {
        var list = await _service.ListarSuscripcionesAsync();
        return Ok(list);
    }

    // PATCH /api/superadmin/suscripciones/{sucursalId}/plan
    [HttpPatch("suscripciones/{sucursalId:guid}/plan")]
    [ProducesResponseType(typeof(SuscripcionSuperAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CambiarPlan(Guid sucursalId, [FromBody] CambiarPlanSuperAdminDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var result = await _service.CambiarPlanSuscripcionAsync(sucursalId, dto);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // POST /api/superadmin/suscripciones/{sucursalId}/extender
    /// <summary>Extiende el vencimiento de la suscripción +30 días desde hoy.</summary>
    [HttpPost("suscripciones/{sucursalId:guid}/extender")]
    [ProducesResponseType(typeof(SuscripcionSuperAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExtenderVencimiento(Guid sucursalId)
    {
        try
        {
            var result = await _service.ExtenderVencimientoAsync(sucursalId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
