using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurneroApp.Application.DTOs.Notificacion;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Enums;

namespace TurneroApp.Api.Controllers;

/// <summary>
/// Notificaciones persistentes del panel admin.
/// Cada usuario consulta solo las notificaciones de su sucursal (claim <c>sucursalId</c>).
/// </summary>
[ApiController]
[Route("api/notificaciones")]
[Authorize]
public class NotificacionController : ControllerBase
{
    private readonly INotificacionRepository _repo;

    public NotificacionController(INotificacionRepository repo) => _repo = repo;

    private Guid? SucursalIdDelToken() =>
        Guid.TryParse(User.FindFirst("sucursalId")?.Value, out var id) ? id : null;

    /// <summary>
    /// Devuelve las notificaciones no leídas de la sucursal del usuario autenticado.
    /// El frontend las carga al montar el panel para recuperar eventos ocurridos
    /// mientras la pestaña estaba cerrada.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<NotificacionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetNoLeidas()
    {
        var sucursalId = SucursalIdDelToken();
        if (sucursalId is null)
            return Unauthorized(new { message = "Token sin sucursalId." });

        var notifs = await _repo.GetNoLeidasBySucursalAsync(sucursalId.Value);

        var dtos = notifs.Select(n => new NotificacionDto
        {
            Id           = n.Id.ToString(),
            Tipo         = n.Tipo == TipoNotificacion.NuevoTurno ? "nuevo" : "cancelado",
            Mensaje      = n.Mensaje,
            FechaCreacion = n.FechaCreacion,
            Leida        = n.Leida,
        });

        return Ok(dtos);
    }

    /// <summary>
    /// Marca todas las notificaciones no leídas de la sucursal como leídas.
    /// Se llama cuando el usuario abre el panel de la campanita.
    /// </summary>
    [HttpPatch("leer")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarcarLeidas()
    {
        var sucursalId = SucursalIdDelToken();
        if (sucursalId is null)
            return Unauthorized(new { message = "Token sin sucursalId." });

        await _repo.MarcarTodasLeidasAsync(sucursalId.Value);
        return NoContent();
    }

    /// <summary>
    /// Elimina todas las notificaciones de la sucursal ("Limpiar todo").
    /// </summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Limpiar()
    {
        var sucursalId = SucursalIdDelToken();
        if (sucursalId is null)
            return Unauthorized(new { message = "Token sin sucursalId." });

        await _repo.EliminarBySucursalAsync(sucursalId.Value);
        return NoContent();
    }
}
