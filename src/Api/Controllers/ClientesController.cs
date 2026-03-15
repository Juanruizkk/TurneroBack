using Microsoft.AspNetCore.Mvc;
using TurneroApp.Application.DTOs.Cliente;
using TurneroApp.Application.Services;

namespace TurneroApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ClientesController : ControllerBase
{
    private readonly ClienteService _clienteService;

    public ClientesController(ClienteService clienteService)
    {
        _clienteService = clienteService;
    }

    /// <summary>
    /// Obtiene clientes. Puede filtrarse por sucursal, teléfono o email.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ClienteResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? sucursalId,
        [FromQuery] string? telefono,
        [FromQuery] string? email)
    {
        if (sucursalId.HasValue && !string.IsNullOrWhiteSpace(telefono))
        {
            var porTel = await _clienteService.BuscarPorTelefonoAsync(sucursalId.Value, telefono);
            return Ok(porTel is null ? Enumerable.Empty<ClienteResponseDto>() : new[] { porTel });
        }

        if (sucursalId.HasValue && !string.IsNullOrWhiteSpace(email))
        {
            var porEmail = await _clienteService.BuscarPorEmailAsync(sucursalId.Value, email);
            return Ok(porEmail is null ? Enumerable.Empty<ClienteResponseDto>() : new[] { porEmail });
        }

        if (sucursalId.HasValue)
        {
            var porSucursal = await _clienteService.GetBySucursalAsync(sucursalId.Value);
            return Ok(porSucursal);
        }

        var todos = await _clienteService.GetAllAsync();
        return Ok(todos);
    }

    /// <summary>
    /// Obtiene un cliente por Id.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ClienteResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var cliente = await _clienteService.GetByIdAsync(id);
        if (cliente is null) return NotFound(new { message = $"Cliente con Id {id} no encontrado." });
        return Ok(cliente);
    }

    /// <summary>
    /// Crea un nuevo cliente.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ClienteResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateClienteDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var cliente = await _clienteService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = cliente.Id }, cliente);
    }

    /// <summary>
    /// Actualiza un cliente existente.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ClienteResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClienteDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var cliente = await _clienteService.UpdateAsync(id, dto);
        if (cliente is null) return NotFound(new { message = $"Cliente con Id {id} no encontrado." });
        return Ok(cliente);
    }

    /// <summary>
    /// Elimina un cliente.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _clienteService.DeleteAsync(id);
        if (!deleted) return NotFound(new { message = $"Cliente con Id {id} no encontrado." });
        return NoContent();
    }
}
