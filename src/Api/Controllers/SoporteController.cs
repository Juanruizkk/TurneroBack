using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TurneroApp.Application.DTOs.Soporte;
using TurneroApp.Application.Interfaces;

namespace TurneroApp.Api.Controllers;

[ApiController]
[Route("api/soporte")]
[Produces("application/json")]
public class SoporteController : ControllerBase
{
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;
    private readonly ISucursalRepository _sucursalRepo;
    private readonly ILogger<SoporteController> _logger;

    public SoporteController(
        IEmailService emailService,
        IConfiguration config,
        ISucursalRepository sucursalRepo,
        ILogger<SoporteController> logger)
    {
        _emailService = emailService;
        _config       = config;
        _sucursalRepo = sucursalRepo;
        _logger       = logger;
    }

    // POST /api/soporte/reportar
    [HttpPost("reportar")]
    [AllowAnonymous]
    [EnableRateLimiting("soporte")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Reportar([FromBody] ReporteProblemaDto dto, CancellationToken ct)
    {
        var soporteEmail = _config["Soporte:Email"] ?? "juanignacioruizr@gmail.com";
        var userAgent    = Request.Headers.UserAgent.ToString();

        try
        {
            await _emailService.EnviarReporteProblemaAsync(
                soporteEmail,
                dto.Descripcion,
                dto.EmailContacto,
                dto.UrlOrigen,
                string.IsNullOrWhiteSpace(userAgent) ? null : userAgent);
        }
        catch (Exception ex)
        {
            // No exponemos el detalle al visitante, pero lo dejamos en el log.
            _logger.LogError(ex, "Falló el envío del reporte de problema");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "No pudimos enviar tu reporte ahora. Intentá de nuevo en un rato." });
        }

        return NoContent();
    }

    // POST /api/soporte/solicitar-ampliacion
    [HttpPost("solicitar-ampliacion")]
    [Authorize]
    [EnableRateLimiting("soporte")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> SolicitarAmpliacion([FromBody] SolicitudAmpliacionDto dto, CancellationToken ct)
    {
        var soporteEmail = _config["Soporte:Email"] ?? "juanignacioruizr@gmail.com";

        var nombre = $"{User.FindFirstValue("nombre")} {User.FindFirstValue("apellido")}".Trim();
        if (string.IsNullOrWhiteSpace(nombre)) nombre = "Usuario";
        var email = User.FindFirstValue(ClaimTypes.Email)
                    ?? User.FindFirstValue("email")
                    ?? "—";
        var sucursal = dto.SucursalNombre;
        if (string.IsNullOrWhiteSpace(sucursal) && Guid.TryParse(User.FindFirstValue("sucursalId"), out var sucId))
            sucursal = (await _sucursalRepo.GetByIdAsync(sucId))?.Nombre;
        if (string.IsNullOrWhiteSpace(sucursal)) sucursal = "Sucursal sin nombre";
        var turnosMax = dto.MaxTurnosMes?.ToString() ?? "∞";
        var usoResumen = $"Usuarios: {dto.UsuariosActuales}/{dto.MaxUsuarios} · Turnos del mes: {dto.TurnosMesActual}/{turnosMax}";

        try
        {
            await _emailService.EnviarSolicitudAmpliacionAsync(
                soporteEmail, sucursal, nombre, email, usoResumen, dto.Mensaje);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló el envío de la solicitud de ampliación");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "No pudimos enviar tu solicitud ahora. Intentá de nuevo en un rato." });
        }

        return NoContent();
    }
}
