using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using TurneroApp.Api.Options;
using TurneroApp.Api.Services;
using TurneroApp.Application.DTOs.Auth;
using TurneroApp.Application.DTOs.Demo;
using TurneroApp.Application.Exceptions;
using TurneroApp.Application.Interfaces;

namespace TurneroApp.Api.Controllers;

[ApiController]
[Route("api/demo")]
[Produces("application/json")]
public class DemoController : ControllerBase
{
    private readonly IDemoTenantService _demoService;
    private readonly JwtService _jwtService;
    private readonly DemoOptions _options;

    public DemoController(IDemoTenantService demoService, JwtService jwtService, IOptions<DemoOptions> options)
    {
        _demoService = demoService;
        _jwtService  = jwtService;
        _options     = options.Value;
    }

    // POST /api/demo/crear
    [HttpPost("crear")]
    [AllowAnonymous]
    [EnableRateLimiting("demo")]
    [ProducesResponseType(typeof(DemoSesionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Crear(CancellationToken ct)
    {
        if (!_options.Enabled) return NotFound();

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        try
        {
            var res = await _demoService.CrearAsync(ip, ct);

            var usuarios = res.Usuarios.Select(u =>
            {
                var resp = _jwtService.GenerarRespuesta(new UsuarioAutenticadoDto
                {
                    Rol          = u.Rol,
                    Nombre       = u.Nombre,
                    Apellido     = u.Apellido,
                    Email        = u.Email,
                    EsSuperAdmin = false,
                    EsOwner      = u.EsOwner,
                    UserId       = u.UsuarioId,
                    SucursalId   = u.SucursalId,
                    Permisos     = u.Permisos,
                });
                return new DemoUsuarioDto
                {
                    Rol     = u.EsOwner ? "Owner" : u.Rol,
                    Nombre  = $"{u.Nombre} {u.Apellido}".Trim(),
                    EsOwner = u.EsOwner,
                    Token   = resp.Token!,
                };
            }).ToList();

            return Ok(new DemoSesionDto
            {
                SucursalId = res.SucursalId,
                ExpiraEn   = res.ExpiraEn,
                Usuarios   = usuarios,
            });
        }
        catch (DemoLimiteException ex)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { message = ex.Message });
        }
    }
}
