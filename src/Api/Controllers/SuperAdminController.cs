using Microsoft.AspNetCore.Mvc;
using TurneroApp.Application.DTOs.SuperAdmin;
using TurneroApp.Application.Services;

namespace TurneroApp.Api.Controllers;

[ApiController]
[Route("api/superadmin")]
[Produces("application/json")]
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
}
