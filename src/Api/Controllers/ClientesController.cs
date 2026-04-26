using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurneroApp.Application.DTOs.Cliente;
using TurneroApp.Application.Exceptions;
using TurneroApp.Application.Services;

namespace TurneroApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class ClientesController : ControllerBase
{
    private readonly ClienteService _clienteService;
    private readonly TurnoService _turnoService;

    public ClientesController(ClienteService clienteService, TurnoService turnoService)
    {
        _clienteService = clienteService;
        _turnoService = turnoService;
    }

    /// <summary>
    /// Obtiene clientes activos. Puede filtrarse por sucursal, teléfono o email.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ClienteResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? sucursalId,
        [FromQuery] string? q,
        [FromQuery] string? telefono,
        [FromQuery] string? email)
    {
        if (sucursalId.HasValue && !string.IsNullOrWhiteSpace(q))
        {
            var resultados = await _clienteService.BuscarAsync(sucursalId.Value, q);
            return Ok(resultados);
        }

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
    /// Obtiene clientes archivados de una sucursal.
    /// </summary>
    [HttpGet("archivados")]
    [ProducesResponseType(typeof(IEnumerable<ClienteResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetArchivados([FromQuery] Guid sucursalId)
    {
        var lista = await _clienteService.GetArchivedBySucursalAsync(sucursalId);
        return Ok(lista);
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
    /// Obtiene la ficha completa de un cliente: resumen, historial y próximos turnos.
    /// </summary>
    [HttpGet("{id:guid}/ficha")]
    [ProducesResponseType(typeof(ClienteFichaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFicha(Guid id)
    {
        var ficha = await _clienteService.GetFichaAsync(id);
        return ficha is null ? NotFound() : Ok(ficha);
    }

    /// <summary>
    /// Obtiene el token de gestión de un turno para un cliente.
    /// </summary>
    [HttpGet("{id:guid}/turno-link/{turnoId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLinkGestionTurno(Guid id, Guid turnoId)
    {
        var turno = await _turnoService.GetByIdAsync(turnoId);
        if (turno is null || turno.ClienteId != id) return NotFound();
        return Ok(new { token = turno.TokenGestion, turnoId });
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

        try
        {
            var cliente = await _clienteService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = cliente.Id }, cliente);
        }
        catch (InvalidOperationException e)
        {
            return BadRequest(new { message = e.Message });
        }
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

        try
        {
            var cliente = await _clienteService.UpdateAsync(id, dto);
            if (cliente is null) return NotFound(new { message = $"Cliente con Id {id} no encontrado." });
            return Ok(cliente);
        }
        catch (InvalidOperationException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    /// <summary>
    /// Archiva un cliente (soft delete). El cliente deja de aparecer en búsquedas y selectors.
    /// </summary>
    [HttpPost("{id:guid}/archivar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archivar(Guid id)
    {
        try
        {
            await _clienteService.ArchivarCliente(id);
            return NoContent();
        }
        catch (KeyNotFoundException e) { return NotFound(new { message = e.Message }); }
    }

    /// <summary>
    /// Desarchiva un cliente previamente archivado.
    /// </summary>
    [HttpPost("{id:guid}/desarchivar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Desarchivar(Guid id)
    {
        try
        {
            await _clienteService.DesarchivarCliente(id);
            return NoContent();
        }
        catch (KeyNotFoundException e) { return NotFound(new { message = e.Message }); }
    }

    /// <summary>
    /// Elimina permanentemente un cliente. Solo permitido si no tiene historial operativo ni pagos.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _clienteService.EliminarCliente(id);
            return NoContent();
        }
        catch (KeyNotFoundException e) { return NotFound(new { message = e.Message }); }
        catch (ClienteNoSePuedeEliminarException e) { return Conflict(new { message = e.Message }); }
    }

    /// <summary>
    /// Stub: envío de recordatorio por WhatsApp (pendiente de integración).
    /// </summary>
    [HttpPost("{id:guid}/recordatorio-whatsapp")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult EnviarRecordatorioWhatsApp(Guid id)
        => Ok(new { message = "Función de WhatsApp pendiente de integración.", clienteId = id });
}
