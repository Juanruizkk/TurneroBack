using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurneroApp.Application.DTOs.Ajustes;
using TurneroApp.Application.DTOs.Sucursal;
using TurneroApp.Application.Services;

namespace TurneroApp.Api.Controllers;

[ApiController]
[Route("api/ajustes")]
[Authorize]
public class AjustesController : ControllerBase
{
    private readonly AjustesService   _service;
    private readonly SucursalService  _sucursalService;

    public AjustesController(AjustesService service, SucursalService sucursalService)
    {
        _service         = service;
        _sucursalService = sucursalService;
    }

    /// <summary>
    /// Verifica que el usuario autenticado tenga acceso a la sucursal solicitada.
    /// Los SuperAdmins pueden acceder a cualquier sucursal.
    /// Devuelve ForbidResult si el acceso no está permitido, null si está OK.
    /// </summary>
    private IActionResult? ValidarAccesoSucursal(Guid sucursalId)
    {
        var esSuperAdmin = User.FindFirstValue("esSuperAdmin") == "true";
        if (esSuperAdmin) return null;

        var claimSucursalId = User.FindFirstValue("sucursalId");
        if (!Guid.TryParse(claimSucursalId, out var tokenSucursalId) || tokenSucursalId != sucursalId)
            return Forbid();

        return null;
    }

    /// <summary>Obtiene los ajustes completos de una sucursal.</summary>
    [HttpGet]
    public async Task<IActionResult> Obtener([FromQuery] Guid sucursalId)
    {
        var check = ValidarAccesoSucursal(sucursalId);
        if (check is not null) return check;

        try
        {
            var result = await _service.ObtenerAsync(sucursalId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Actualiza los ajustes de una sucursal.</summary>
    [HttpPut]
    public async Task<IActionResult> Actualizar([FromQuery] Guid sucursalId, [FromBody] ActualizarAjustesDto dto)
    {
        var check = ValidarAccesoSucursal(sucursalId);
        if (check is not null) return check;

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _service.ActualizarAsync(sucursalId, dto);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // ─── Imagen endpoints ────────────────────────────────────────────────────

    /// <summary>
    /// Sube la imagen de logo o background de la sucursal a Cloudinary.
    /// </summary>
    /// <param name="tipo">"logo" | "background"</param>
    [HttpPost("imagen")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SubirImagen(
        [FromQuery] Guid sucursalId,
        [FromQuery] string tipo,
        IFormFile? file)
    {
        var check = ValidarAccesoSucursal(sucursalId);
        if (check is not null) return check;

        if (file is null || file.Length == 0)
            return BadRequest(new { message = "No se recibió ningún archivo." });

        if (!tipo.Equals("logo", StringComparison.OrdinalIgnoreCase) &&
            !tipo.Equals("background", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "El parámetro 'tipo' debe ser 'logo' o 'background'." });

        try
        {
            using var stream = file.OpenReadStream();
            var result = await _sucursalService.SubirImagenAsync(
                sucursalId, stream, file.FileName, file.ContentType, tipo);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Elimina la imagen de logo o background de la sucursal (Cloudinary + DB).
    /// </summary>
    /// <param name="tipo">"logo" | "background"</param>
    [HttpDelete("imagen")]
    public async Task<IActionResult> EliminarImagen(
        [FromQuery] Guid sucursalId,
        [FromQuery] string tipo)
    {
        var check = ValidarAccesoSucursal(sucursalId);
        if (check is not null) return check;

        if (!tipo.Equals("logo", StringComparison.OrdinalIgnoreCase) &&
            !tipo.Equals("background", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "El parámetro 'tipo' debe ser 'logo' o 'background'." });

        try
        {
            await _sucursalService.EliminarImagenAsync(sucursalId, tipo);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ─── Slug endpoints ───────────────────────────────────────────────────────

    /// <summary>Devuelve el slug actual de una sucursal.</summary>
    [HttpGet("slug")]
    public async Task<IActionResult> ObtenerSlug([FromQuery] Guid sucursalId)
    {
        var check = ValidarAccesoSucursal(sucursalId);
        if (check is not null) return check;

        try
        {
            var slug = await _sucursalService.ObtenerSlugAsync(sucursalId);
            return Ok(new { slug });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Verifica si un slug está disponible para ser usado por la sucursal indicada.
    /// </summary>
    [HttpGet("slug-disponible")]
    public async Task<IActionResult> SlugDisponible(
        [FromQuery] Guid sucursalId,
        [FromQuery] string slug)
    {
        var check = ValidarAccesoSucursal(sucursalId);
        if (check is not null) return check;

        if (string.IsNullOrWhiteSpace(slug))
            return BadRequest(new { message = "El slug no puede estar vacío." });

        var disponible = await _sucursalService.SlugDisponibleAsync(slug, sucursalId);
        return Ok(new { disponible });
    }

    /// <summary>Actualiza el slug de una sucursal.</summary>
    [HttpPatch("slug")]
    public async Task<IActionResult> ActualizarSlug(
        [FromQuery] Guid sucursalId,
        [FromBody] ActualizarSlugDto dto)
    {
        var check = ValidarAccesoSucursal(sucursalId);
        if (check is not null) return check;

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _sucursalService.ActualizarSlugAsync(sucursalId, dto.Slug);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
