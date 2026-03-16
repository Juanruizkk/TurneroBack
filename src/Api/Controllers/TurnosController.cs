using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TurneroApp.Application.DTOs.Turno;
using TurneroApp.Application.Services;

namespace TurneroApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class TurnosController : ControllerBase
{
    private readonly TurnoService        _turnoService;
    private readonly AdjuntoTurnoService _adjuntoService;

    public TurnosController(TurnoService turnoService, AdjuntoTurnoService adjuntoService)
    {
        _turnoService   = turnoService;
        _adjuntoService = adjuntoService;
    }

    /// <summary>
    /// Obtiene todos los turnos. Puede filtrarse por sucursal.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TurnoResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] Guid? sucursalId)
    {
        if (sucursalId.HasValue)
        {
            var porSucursal = await _turnoService.GetBySucursalAsync(sucursalId.Value);
            return Ok(porSucursal);
        }

        var turnos = await _turnoService.GetAllAsync();
        return Ok(turnos);
    }

    /// <summary>
    /// Obtiene un turno por Id.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TurnoResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var turno = await _turnoService.GetByIdAsync(id);
        if (turno is null) return NotFound(new { message = $"Turno con Id {id} no encontrado." });
        return Ok(turno);
    }

    /// <summary>
    /// Obtiene un turno por su token de gestión (para uso de clientes).
    /// </summary>
    [HttpGet("by-token/{token}")]
    [ProducesResponseType(typeof(TurnoResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByToken(string token)
    {
        var turno = await _turnoService.GetByTokenAsync(token);
        if (turno is null) return NotFound(new { message = "Turno no encontrado para el token proporcionado." });
        return Ok(turno);
    }

    /// <summary>
    /// Crea un nuevo turno. Valida que no existan conflictos de horario con el profesional.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TurnoResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateTurnoDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var turno = await _turnoService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = turno.Id }, turno);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("conflicto") || ex.Message.Contains("horario"))
        {
            return Conflict(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Actualiza el estado y/o datos de un turno.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TurnoResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTurnoDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var turno = await _turnoService.UpdateEstadoAsync(id, dto);
            if (turno is null) return NotFound(new { message = $"Turno con Id {id} no encontrado." });
            return Ok(turno);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("conflicto") || ex.Message.Contains("horario"))
        {
            return Conflict(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Elimina un turno.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _turnoService.DeleteAsync(id);
        if (!deleted) return NotFound(new { message = $"Turno con Id {id} no encontrado." });
        return NoContent();
    }

    // ─── Comentarios ─────────────────────────────────────────────────────────────

    /// <summary>Lista todos los comentarios de un turno.</summary>
    [HttpGet("{turnoId:guid}/comentarios")]
    [ProducesResponseType(typeof(IEnumerable<ComentarioTurnoResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetComentarios(Guid turnoId)
    {
        var comentarios = await _turnoService.GetComentariosAsync(turnoId);
        return Ok(comentarios);
    }

    /// <summary>Agrega un comentario a un turno.</summary>
    [HttpPost("{turnoId:guid}/comentarios")]
    [ProducesResponseType(typeof(ComentarioTurnoResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CrearComentario(Guid turnoId, [FromBody] CrearComentarioDto dto)
    {
        var userIdStr = User.FindFirst("userId")?.Value;
        if (!Guid.TryParse(userIdStr, out var autorId))
            return Unauthorized(new { message = "No se pudo identificar al usuario." });

        try
        {
            var comentario = await _turnoService.CrearComentarioAsync(turnoId, autorId, dto.Texto);
            return CreatedAtAction(nameof(GetComentarios), new { turnoId }, comentario);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ─── Adjuntos ─────────────────────────────────────────────────────────────────

    /// <summary>Lista todos los archivos adjuntos de un turno.</summary>
    [HttpGet("{turnoId:guid}/adjuntos")]
    [ProducesResponseType(typeof(IEnumerable<AdjuntoTurnoResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAdjuntos(Guid turnoId)
    {
        var adjuntos = await _adjuntoService.ListarAsync(turnoId);
        return Ok(adjuntos);
    }

    /// <summary>Sube un archivo (imagen o PDF) y lo vincula al turno.</summary>
    [HttpPost("{turnoId:guid}/adjuntos")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(AdjuntoTurnoResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SubirAdjunto(Guid turnoId, [FromForm] IFormFile file)
    {
        var userIdStr = User.FindFirst("userId")?.Value;
        if (!Guid.TryParse(userIdStr, out var autorId))
            return Unauthorized(new { message = "No se pudo identificar al usuario." });

        if (file is null || file.Length == 0)
            return BadRequest(new { message = "No se proporcionó ningún archivo." });

        try
        {
            using var stream = file.OpenReadStream();
            var adjunto = await _adjuntoService.SubirAsync(
                turnoId, autorId, stream, file.FileName, file.ContentType);
            return CreatedAtAction(nameof(GetAdjuntos), new { turnoId }, adjunto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Elimina un adjunto de la DB y borra el asset físico en Cloudinary.</summary>
    [HttpDelete("{turnoId:guid}/adjuntos/{adjuntoId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EliminarAdjunto(Guid turnoId, Guid adjuntoId)
    {
        var deleted = await _adjuntoService.EliminarAsync(adjuntoId);
        if (!deleted) return NotFound(new { message = "Adjunto no encontrado." });
        return NoContent();
    }

    /// <summary>Elimina un comentario. Solo el autor puede eliminarlo.</summary>
    [HttpDelete("{turnoId:guid}/comentarios/{comentarioId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EliminarComentario(Guid turnoId, Guid comentarioId)
    {
        var userIdStr = User.FindFirst("userId")?.Value;
        if (!Guid.TryParse(userIdStr, out var autorId))
            return Unauthorized(new { message = "No se pudo identificar al usuario." });

        try
        {
            var deleted = await _turnoService.EliminarComentarioAsync(comentarioId, autorId);
            if (!deleted) return NotFound(new { message = "Comentario no encontrado." });
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
