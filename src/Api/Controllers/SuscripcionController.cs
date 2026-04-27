using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurneroApp.Application.Interfaces;
using TurneroApp.Application.Services;

namespace TurneroApp.Api.Controllers;

[ApiController]
[Route("api/suscripcion")]
[Authorize]
public class SuscripcionController : ControllerBase
{
    private readonly SuscripcionService      _service;
    private readonly IPagoSuscripcionService _pagoService;

    public SuscripcionController(SuscripcionService service, IPagoSuscripcionService pagoService)
    {
        _service     = service;
        _pagoService = pagoService;
    }

    private IActionResult? ValidarAccesoSucursal(Guid sucursalId)
    {
        var esSuperAdmin = User.FindFirstValue("esSuperAdmin") == "true";
        if (esSuperAdmin) return null;

        var claimSucursalId = User.FindFirstValue("sucursalId");
        if (!Guid.TryParse(claimSucursalId, out var tokenSucursalId) || tokenSucursalId != sucursalId)
            return Forbid();

        return null;
    }

    /// <summary>Obtiene el estado actual de la suscripción de una sucursal.</summary>
    [HttpGet]
    public async Task<IActionResult> Obtener([FromQuery] Guid sucursalId)
    {
        var check = ValidarAccesoSucursal(sucursalId);
        if (check is not null) return check;

        var result = await _service.ObtenerAsync(sucursalId);
        return Ok(result);
    }

    /// <summary>
    /// (SuperAdmin) Procesa manualmente un pago de suscripción aprobado por MP.
    /// Usar cuando el webhook no llegó (ngrok caído, etc.) y el pago ya fue acreditado.
    /// </summary>
    [HttpPost("procesar-pago/{paymentId:long}")]
    public async Task<IActionResult> ProcesarPagoManual([FromRoute] long paymentId)
    {
        var esSuperAdmin = User.FindFirstValue("esSuperAdmin") == "true";
        if (!esSuperAdmin) return Forbid();

        await _pagoService.ProcesarWebhookAsync(paymentId);
        return Ok(new { mensaje = "Pago procesado correctamente." });
    }
}
