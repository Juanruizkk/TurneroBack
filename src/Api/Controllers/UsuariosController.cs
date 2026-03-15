using Microsoft.AspNetCore.Mvc;
using TurneroApp.Application.DTOs.Usuario;
using TurneroApp.Application.Services;

namespace TurneroApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class UsuariosController : ControllerBase
{
    private readonly UsuarioService _usuarioService;

    public UsuariosController(UsuarioService usuarioService)
    {
        _usuarioService = usuarioService;
    }

    // GET /api/usuarios?sucursalId={id}
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<UsuarioResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetBySucursal([FromQuery] Guid sucursalId)
    {
        if (sucursalId == Guid.Empty)
            return BadRequest(new { message = "El parámetro sucursalId es requerido." });

        var usuarios = await _usuarioService.GetBySucursalAsync(sucursalId);
        return Ok(usuarios);
    }

    // GET /api/usuarios/{id}
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UsuarioResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var usuario = await _usuarioService.GetByIdAsync(id);
            return Ok(usuario);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // POST /api/usuarios
    [HttpPost]
    [ProducesResponseType(typeof(UsuarioResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateUsuarioDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var usuario = await _usuarioService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = usuario.Id }, usuario);
        }
        catch (InvalidOperationException ex)
        {
            // Email duplicado → 409 Conflict
            return Conflict(new { message = ex.Message });
        }
    }

    // PUT /api/usuarios/{id}
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(UsuarioResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUsuarioDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var usuario = await _usuarioService.UpdateAsync(id, dto);
            return Ok(usuario);
        }
        catch (InvalidOperationException ex)
        {
            // Determinar si es "no encontrado" o "email duplicado"
            if (ex.Message.Contains("No se encontró"))
                return NotFound(new { message = ex.Message });

            return Conflict(new { message = ex.Message });
        }
    }

    // POST /api/usuarios/{id}/reenviar-invitacion
    [HttpPost("{id:guid}/reenviar-invitacion")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReenviarInvitacion(Guid id)
    {
        try
        {
            await _usuarioService.ReenviarInvitacionAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("No se encontró")
                ? NotFound(new { message = ex.Message })
                : BadRequest(new { message = ex.Message });
        }
    }

    // DELETE /api/usuarios/{id}
    // Soft delete: cambia el estado a Inactivo, no elimina físicamente.
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _usuarioService.DeleteAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
