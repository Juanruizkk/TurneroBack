using Microsoft.AspNetCore.Mvc;
using TurneroApp.Application.DTOs.Publico;
using TurneroApp.Application.Exceptions;
using TurneroApp.Application.Services;

namespace TurneroApp.Api.Controllers;

/// <summary>Endpoints públicos (sin autenticación) para el booking online.</summary>
[ApiController]
[Route("api/publico")]
public class PublicoController : ControllerBase
{
    private readonly PublicoService _service;
    private readonly ServicioService _servicioService;
    private readonly ILogger<PublicoController> _logger;

    public PublicoController(PublicoService service, ServicioService servicioService, ILogger<PublicoController> logger)
    {
        _service         = service;
        _servicioService = servicioService;
        _logger          = logger;
    }


    /// <summary>Servicios activos de la sucursal (para el booking público).</summary>
    [HttpGet("{slug}/servicios")]
    public async Task<IActionResult> Servicios(string slug)
    {
        try
        {
            var perfil = await _service.ObtenerPerfilAsync(slug);
            var servicios = await _servicioService.GetBySucursalAsync(perfil.SucursalId, incluirInactivos: false);
            return Ok(servicios);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Perfil público de la sucursal (nombre, slogan, foto, rubros, horarios).</summary>
    [HttpGet("{slug}/perfil")]
    public async Task<IActionResult> Perfil(string slug)
    {
        try
        {
            var perfil = await _service.ObtenerPerfilAsync(slug);
            return Ok(perfil);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Crea un turno desde el booking público (sin autenticación).
    /// Busca o crea el cliente por teléfono y luego crea el turno.
    /// </summary>
    [HttpPost("{slug}/reservar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reservar(string slug, [FromBody] ReservarTurnoPublicoDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var perfil = await _service.ObtenerPerfilAsync(slug);
            var turno  = await _service.ReservarAsync(perfil.SucursalId, dto);
            return Ok(turno);
        }
        catch (SuscripcionVencidaException ex)
        {
            return StatusCode(402, new { message = ex.Message });
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
    /// Crea un turno desde el booking público usando sucursalId directo (legacy / VITE_SUCURSAL_ID).
    /// </summary>
    [HttpPost("reservar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReservarPorId([FromQuery] Guid sucursalId, [FromBody] ReservarTurnoPublicoDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var turno = await _service.ReservarAsync(sucursalId, dto);
            return Ok(turno);
        }
        catch (SuscripcionVencidaException ex)
        {
            return StatusCode(402, new { message = ex.Message });
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
    /// Slots horarios disponibles para una fecha y servicio dados.
    /// Devuelve lista de { hora, profesionalId } para usar al crear el turno.
    /// </summary>
    [HttpGet("{slug}/slots")]
    public async Task<IActionResult> Slots(
        string slug,
        [FromQuery] string fecha,       // "yyyy-MM-dd"
        [FromQuery] Guid servicioId)
    {
        if (!DateOnly.TryParse(fecha, out var dateOnly))
            return BadRequest(new { message = "Formato de fecha inválido. Use yyyy-MM-dd." });

        try
        {
            var slots = await _service.ObtenerSlotsAsync(slug, dateOnly, servicioId);
            return Ok(slots);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener slots para slug={Slug} fecha={Fecha} servicioId={ServicioId}",
                slug, fecha, servicioId);
            return StatusCode(500, new { message = "Error interno al obtener disponibilidad. Intente más tarde." });
        }
    }
}
