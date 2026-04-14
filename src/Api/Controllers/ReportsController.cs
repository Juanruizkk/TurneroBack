using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurneroApp.Application.DTOs.Reportes;
using TurneroApp.Application.Exceptions;
using TurneroApp.Application.Interfaces;
using TurneroApp.Application.Services;

namespace TurneroApp.Api.Controllers;

[ApiController]
[Route("api/reportes")]
[Authorize]
[Produces("application/json")]
public class ReportsController : ControllerBase
{
    private readonly IReportRepository _reportRepo;
    private readonly SuscripcionGuard  _guard;

    public ReportsController(IReportRepository reportRepo, SuscripcionGuard guard)
    {
        _reportRepo = reportRepo;
        _guard      = guard;
    }

    private Guid GetSucursalId()
    {
        var claim = User.FindFirst("sucursalId")?.Value
            ?? throw new UnauthorizedAccessException("Token sin sucursalId.");
        return Guid.Parse(claim);
    }

    // GET /api/reportes/resumen?desde=2026-03-01&hasta=2026-03-31
    [HttpGet("resumen")]
    [ProducesResponseType(typeof(ReporteResumenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Resumen([FromQuery] DateTime desde, [FromQuery] DateTime hasta)
    {
        var sucursalId = GetSucursalId();

        try { await _guard.VerificarActivaAsync(sucursalId); }
        catch (SuscripcionVencidaException ex) { return StatusCode(402, new { message = ex.Message }); }

        try { await _guard.VerificarTieneReportesAsync(sucursalId); }
        catch (FeatureNoPlanException ex) { return StatusCode(403, new { message = ex.Message }); }

        var desdeUtc   = DateTime.SpecifyKind(desde, DateTimeKind.Unspecified);
        var hastaUtc   = DateTime.SpecifyKind(hasta.Date.AddDays(1).AddTicks(-1), DateTimeKind.Unspecified);

        var turnos        = await _reportRepo.GetTurnosAsync(sucursalId, desdeUtc, hastaUtc);
        var ingresos      = await _reportRepo.GetIngresosAsync(sucursalId, desdeUtc, hastaUtc);
        var servicios     = await _reportRepo.GetTopServiciosAsync(sucursalId, desdeUtc, hastaUtc);
        var profesionales = await _reportRepo.GetTopProfesionalesAsync(sucursalId, desdeUtc, hastaUtc);
        var clientes      = await _reportRepo.GetClientesAsync(sucursalId, desdeUtc, hastaUtc);

        return Ok(new ReporteResumenDto
        {
            Turnos           = turnos,
            Ingresos         = ingresos,
            TopServicios     = servicios,
            TopProfesionales = profesionales,
            Clientes         = clientes,
        });
    }

    // GET /api/reportes/turnos?desde=&hasta=
    [HttpGet("turnos")]
    [ProducesResponseType(typeof(ReporteTurnosDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Turnos([FromQuery] DateTime desde, [FromQuery] DateTime hasta)
    {
        var sucursalId = GetSucursalId();

        try { await _guard.VerificarActivaAsync(sucursalId); }
        catch (SuscripcionVencidaException ex) { return StatusCode(402, new { message = ex.Message }); }

        try { await _guard.VerificarTieneReportesAsync(sucursalId); }
        catch (FeatureNoPlanException ex) { return StatusCode(403, new { message = ex.Message }); }

        var desdeUtc = DateTime.SpecifyKind(desde, DateTimeKind.Unspecified);
        var hastaUtc = DateTime.SpecifyKind(hasta.Date.AddDays(1).AddTicks(-1), DateTimeKind.Unspecified);
        return Ok(await _reportRepo.GetTurnosAsync(sucursalId, desdeUtc, hastaUtc));
    }

    // GET /api/reportes/ingresos?desde=&hasta=
    [HttpGet("ingresos")]
    [ProducesResponseType(typeof(ReporteIngresosDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Ingresos([FromQuery] DateTime desde, [FromQuery] DateTime hasta)
    {
        var sucursalId = GetSucursalId();

        try { await _guard.VerificarActivaAsync(sucursalId); }
        catch (SuscripcionVencidaException ex) { return StatusCode(402, new { message = ex.Message }); }

        try { await _guard.VerificarTieneReportesAsync(sucursalId); }
        catch (FeatureNoPlanException ex) { return StatusCode(403, new { message = ex.Message }); }

        var desdeUtc = DateTime.SpecifyKind(desde, DateTimeKind.Unspecified);
        var hastaUtc = DateTime.SpecifyKind(hasta.Date.AddDays(1).AddTicks(-1), DateTimeKind.Unspecified);
        return Ok(await _reportRepo.GetIngresosAsync(sucursalId, desdeUtc, hastaUtc));
    }
}
