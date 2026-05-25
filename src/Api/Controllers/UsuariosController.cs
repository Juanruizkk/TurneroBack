using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurneroApp.Application.DTOs.Usuario;
using TurneroApp.Application.Exceptions;
using TurneroApp.Application.Services;

namespace TurneroApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class UsuariosController : ControllerBase
{
    private readonly UsuarioService  _usuarioService;
    private readonly SuscripcionGuard _guard;

    public UsuariosController(UsuarioService usuarioService, SuscripcionGuard guard)
    {
        _usuarioService = usuarioService;
        _guard          = guard;
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

        try { await _guard.VerificarActivaAsync(dto.SucursalId); }
        catch (SuscripcionVencidaException ex) { return StatusCode(402, new { message = ex.Message }); }

        try
        {
            var usuario = await _usuarioService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = usuario.Id }, usuario);
        }
        catch (InvalidOperationException ex)
        {
            // Email duplicado o límite de plan → 409/400
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

    // PUT /api/usuarios/{id}/rol-estado-permisos
    /// <summary>
    /// Actualiza el rol, estado y permisos de la membresía de un usuario.
    /// Solo accesible por usuarios con rol Administrativo u Owner.
    /// </summary>
    [HttpPut("{id:guid}/rol-estado-permisos")]
    [ProducesResponseType(typeof(UsuarioResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRolEstadoPermisos(Guid id, [FromBody] UpdateRolEstadoPermisosDto dto)
    {
        var esAdmin = User.IsInRole("Administrativo");
        var esOwner = User.FindFirstValue("esOwner") == "true";
        if (!esAdmin && !esOwner)
            return Forbid();

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var usuario = await _usuarioService.UpdateRolEstadoPermisosAsync(id, dto);
            return Ok(usuario);
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("No se encontró"))
                return NotFound(new { message = ex.Message });

            return BadRequest(new { message = ex.Message });
        }
    }

    // POST /api/usuarios/{id}/reenviar-invitacion
    [HttpPost("{id:guid}/reenviar-invitacion")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReenviarInvitacion(Guid id)
    {
        try
        {
            var link = await _usuarioService.ReenviarInvitacionAsync(id);
            return Ok(new { link });
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("No se encontró")
                ? NotFound(new { message = ex.Message })
                : BadRequest(new { message = ex.Message });
        }
    }

    // GET /api/usuarios/{id}/horarios
    [HttpGet("{id:guid}/horarios")]
    [ProducesResponseType(typeof(List<HorarioLaboralDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHorarios(Guid id)
    {
        try
        {
            var horarios = await _usuarioService.GetHorariosAsync(id);
            return Ok(horarios);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // PUT /api/usuarios/{id}/horarios
    [HttpPut("{id:guid}/horarios")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PutHorarios(Guid id, [FromBody] List<HorarioLaboralDto> dtos)
    {
        try
        {
            await _usuarioService.ReplaceHorariosAsync(id, dtos);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // GET /api/usuarios/{id}/servicios
    [HttpGet("{id:guid}/servicios")]
    [ProducesResponseType(typeof(List<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetServicios(Guid id)
    {
        try
        {
            var servicios = await _usuarioService.GetServiciosAsync(id);
            return Ok(servicios);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // PUT /api/usuarios/{id}/servicios
    [HttpPut("{id:guid}/servicios")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PutServicios(Guid id, [FromBody] List<Guid> servicioIds)
    {
        try
        {
            await _usuarioService.ReplaceServiciosAsync(id, servicioIds);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // DELETE /api/usuarios/{id}
    // Soft delete: cambia el estado a Inactivo, no elimina físicamente.
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _usuarioService.DeleteAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("No se encontró"))
                return NotFound(new { message = ex.Message });

            return Conflict(new { message = ex.Message });
        }
    }

    // DELETE /api/usuarios/{id}/hard
    // Hard delete: elimina físicamente la membresía y todos sus datos de configuración.
    [HttpDelete("{id:guid}/hard")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> HardDelete(Guid id)
    {
        try
        {
            await _usuarioService.HardDeleteAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("no encontrado"))
                return NotFound(new { message = ex.Message });

            return Conflict(new { message = ex.Message });
        }
    }
}
