using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurneroApp.Application.DTOs.Caja;
using TurneroApp.Application.Services;

namespace TurneroApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class CajaController : ControllerBase
{
    private readonly CajaService _cajaService;

    public CajaController(CajaService cajaService)
    {
        _cajaService = cajaService;
    }

    // ── Helpers de claims ─────────────────────────────────────────────────────

    private Guid GetUsuarioId()
    {
        // El JWT incluye "userId" y también "sub" con el mismo valor
        var raw = User.FindFirstValue("userId")
               ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return Guid.TryParse(raw, out var id)
            ? id
            : throw new UnauthorizedAccessException("Token inválido: no contiene un userId válido.");
    }

    private string GetRol()
        => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    // ── GET /api/caja/sucursal/{sucursalId} ───────────────────────────────────

    /// <summary>Obtiene la caja asociada a una sucursal.</summary>
    [HttpGet("sucursal/{sucursalId:guid}")]
    [ProducesResponseType(typeof(CajaResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBySucursal(Guid sucursalId)
    {
        var caja = await _cajaService.GetBySucursalAsync(sucursalId);
        if (caja is null)
            return NotFound(new { message = $"No se encontró una caja para la sucursal {sucursalId}." });
        return Ok(caja);
    }

    // ── GET /api/caja/{cajaId}/movimientos ────────────────────────────────────

    /// <summary>Lista los movimientos de una caja con filtros opcionales de fecha.</summary>
    [HttpGet("{cajaId:guid}/movimientos")]
    [ProducesResponseType(typeof(IEnumerable<MovimientoCajaResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMovimientos(
        Guid       cajaId,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta)
    {
        var movimientos = await _cajaService.GetMovimientosAsync(cajaId, desde, hasta);
        return Ok(movimientos);
    }

    // ── POST /api/caja/{cajaId}/movimientos ───────────────────────────────────

    /// <summary>
    /// Registra un movimiento de caja (ingreso, cobro de turno, gasto, retiro o ajuste).
    /// El usuario que registra se extrae del JWT — no se acepta en el body.
    /// Si el tipo es CobroTurno y el rol es Profesional, solo puede cobrar sus propios turnos (403 si no).
    /// </summary>
    [HttpPost("{cajaId:guid}/movimientos")]
    [ProducesResponseType(typeof(MovimientoCajaResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegistrarMovimiento(
        Guid                   cajaId,
        [FromBody] RegistrarMovimientoDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // Validación de negocio básica en el controller (rápida, sin ir a la BD)
        if (dto.TipoMovimiento == Domain.Enums.TipoMovimientoCaja.CobroTurno && !dto.TurnoId.HasValue)
            return BadRequest(new { message = "TurnoId es obligatorio para movimientos de tipo CobroTurno." });

        Guid   usuarioId;
        string rol;
        try
        {
            usuarioId = GetUsuarioId();
            rol       = GetRol();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }

        try
        {
            var movimiento = await _cajaService.RegistrarMovimientoAsync(cajaId, dto, usuarioId, rol);
            return CreatedAtAction(
                nameof(GetMovimientos),
                new { cajaId = movimiento.CajaId },
                movimiento);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // ── GET /api/caja/{cajaId}/resumen-liquidaciones ──────────────────────────

    /// <summary>
    /// Resumen financiero del período y liquidaciones por profesional.
    /// Si se omite 'hasta', el período cubre únicamente el día de 'desde' (cierre diario).
    /// </summary>
    [HttpGet("{cajaId:guid}/resumen-liquidaciones")]
    [Authorize(Roles = "Administrativo")]
    [ProducesResponseType(typeof(ResumenLiquidacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResumenLiquidaciones(
        Guid       cajaId,
        [FromQuery] DateTime  desde,
        [FromQuery] DateTime? hasta = null)
    {
        if (desde == default)
            return BadRequest(new { message = "El parámetro 'desde' es obligatorio." });

        if (hasta.HasValue && hasta.Value.Date < desde.Date)
            return BadRequest(new { message = "La fecha 'hasta' no puede ser anterior a 'desde'." });

        try
        {
            var resumen = await _cajaService.GetResumenLiquidacionesAsync(cajaId, desde, hasta);
            return Ok(resumen);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // ── GET /api/caja/{cajaId}/sesiones/activa ────────────────────────────────

    /// <summary>Obtiene la sesión activa de la caja, o 204 si no hay ninguna.</summary>
    [HttpGet("{cajaId:guid}/sesiones/activa")]
    [ProducesResponseType(typeof(CajaSesionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetSesionActiva(Guid cajaId)
    {
        var sesion = await _cajaService.GetSesionActivaAsync(cajaId);
        if (sesion is null) return NoContent();
        return Ok(sesion);
    }

    // ── GET /api/caja/{cajaId}/sesiones ───────────────────────────────────────

    /// <summary>Historial de sesiones de la caja, ordenadas por fecha de apertura descendente.</summary>
    [HttpGet("{cajaId:guid}/sesiones")]
    [ProducesResponseType(typeof(IEnumerable<CajaSesionResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistorialSesiones(Guid cajaId)
    {
        var sesiones = await _cajaService.GetHistorialSesionesAsync(cajaId);
        return Ok(sesiones);
    }

    // ── POST /api/caja/{cajaId}/sesiones/abrir ────────────────────────────────

    /// <summary>Abre una nueva sesión de caja. Retorna 400 si ya hay una sesión activa.</summary>
    [HttpPost("{cajaId:guid}/sesiones/abrir")]
    [ProducesResponseType(typeof(CajaSesionResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AbrirSesion(Guid cajaId, [FromBody] AbrirSesionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        Guid usuarioId;
        try { usuarioId = GetUsuarioId(); }
        catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }

        try
        {
            var sesion = await _cajaService.AbrirSesionAsync(cajaId, dto, usuarioId);
            return CreatedAtAction(nameof(GetSesionActiva), new { cajaId }, sesion);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ── POST /api/caja/{cajaId}/sesiones/cerrar ───────────────────────────────

    /// <summary>Cierra la sesión activa de la caja. Retorna 400 si no hay sesión activa.</summary>
    [HttpPost("{cajaId:guid}/sesiones/cerrar")]
    [ProducesResponseType(typeof(CajaSesionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CerrarSesion(Guid cajaId, [FromBody] CerrarSesionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        Guid usuarioId;
        try { usuarioId = GetUsuarioId(); }
        catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }

        try
        {
            var sesion = await _cajaService.CerrarSesionAsync(cajaId, dto, usuarioId);
            return Ok(sesion);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
