using Microsoft.AspNetCore.Mvc;
using TurneroApp.Application.DTOs.Servicio;
using TurneroApp.Application.Exceptions;
using TurneroApp.Application.Services;

namespace TurneroApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ServiciosController : ControllerBase
{
    private readonly ServicioService  _servicioService;
    private readonly SuscripcionGuard _guard;

    public ServiciosController(ServicioService servicioService, SuscripcionGuard guard)
    {
        _servicioService = servicioService;
        _guard           = guard;
    }

    /// <summary>
    /// Obtiene todos los servicios. Puede filtrarse por sucursal.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ServicioResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] Guid? sucursalId, [FromQuery] bool incluirInactivos = false)
    {
        if (sucursalId.HasValue)
        {
            var porSucursal = await _servicioService.GetBySucursalAsync(sucursalId.Value, incluirInactivos);
            return Ok(porSucursal);
        }

        var servicios = await _servicioService.GetAllAsync();
        return Ok(servicios);
    }

    /// <summary>
    /// Obtiene un servicio por Id.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ServicioResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var servicio = await _servicioService.GetByIdAsync(id);
        if (servicio is null) return NotFound(new { message = $"Servicio con Id {id} no encontrado." });
        return Ok(servicio);
    }

    /// <summary>
    /// Crea un nuevo servicio.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ServicioResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateServicioDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try { await _guard.VerificarActivaAsync(dto.SucursalId); }
        catch (SuscripcionVencidaException ex) { return StatusCode(402, new { message = ex.Message }); }

        var servicio = await _servicioService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = servicio.Id }, servicio);
    }

    /// <summary>
    /// Actualiza un servicio existente.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ServicioResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateServicioDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var servicio = await _servicioService.UpdateAsync(id, dto);
        if (servicio is null) return NotFound(new { message = $"Servicio con Id {id} no encontrado." });
        return Ok(servicio);
    }

    /// <summary>
    /// Desactiva un servicio (soft-delete). Los turnos existentes se mantienen intactos.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var ok = await _servicioService.DesactivarAsync(id);
        if (!ok) return NotFound(new { message = $"Servicio con Id {id} no encontrado." });
        return NoContent();
    }
}
