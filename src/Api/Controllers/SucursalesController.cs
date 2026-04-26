using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurneroApp.Application.DTOs.Sucursal;
using TurneroApp.Application.Services;

namespace TurneroApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class SucursalesController : ControllerBase
{
    private readonly SucursalService _sucursalService;

    public SucursalesController(SucursalService sucursalService)
    {
        _sucursalService = sucursalService;
    }

    /// <summary>
    /// Obtiene todas las sucursales. Con ?soloActivas=true filtra solo las activas.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SucursalResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] bool soloActivas = false)
    {
        var sucursales = soloActivas
            ? await _sucursalService.GetActivasAsync()
            : await _sucursalService.GetAllAsync();

        return Ok(sucursales);
    }

    /// <summary>
    /// Obtiene una sucursal por Id.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SucursalResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var sucursal = await _sucursalService.GetByIdAsync(id);
        if (sucursal is null) return NotFound(new { message = $"Sucursal con Id {id} no encontrada." });
        return Ok(sucursal);
    }

    /// <summary>
    /// Crea una nueva sucursal.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SucursalResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateSucursalDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var sucursal = await _sucursalService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = sucursal.Id }, sucursal);
    }

    /// <summary>
    /// Actualiza una sucursal existente.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(SucursalResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSucursalDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var sucursal = await _sucursalService.UpdateAsync(id, dto);
        if (sucursal is null) return NotFound(new { message = $"Sucursal con Id {id} no encontrada." });
        return Ok(sucursal);
    }

    /// <summary>
    /// Elimina una sucursal.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _sucursalService.DeleteAsync(id);
        if (!deleted) return NotFound(new { message = $"Sucursal con Id {id} no encontrada." });
        return NoContent();
    }
}
