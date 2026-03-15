using Microsoft.AspNetCore.Mvc;
using TurneroApp.Application.DTOs.Auth;
using TurneroApp.Application.Services;
using TurneroApp.Api.Services;

namespace TurneroApp.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly AuthService      _authService;
    private readonly JwtService       _jwtService;
    private readonly InvitacionService _invitacionService;

    public AuthController(
        AuthService authService,
        JwtService jwtService,
        InvitacionService invitacionService)
    {
        _authService       = authService;
        _jwtService        = jwtService;
        _invitacionService = invitacionService;
    }

    // POST /api/auth/login
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var usuario   = await _authService.ValidarAsync(dto);
            var respuesta = _jwtService.GenerarRespuesta(usuario);
            return Ok(respuesta);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    // GET /api/auth/invitacion/{token}
    // Devuelve info pública para mostrar en la pantalla de bienvenida
    [HttpGet("invitacion/{token}")]
    [ProducesResponseType(typeof(InfoInvitacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InfoInvitacion(string token)
    {
        try
        {
            var info = await _invitacionService.ObtenerInfoAsync(token);
            return Ok(info);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // POST /api/auth/invitacion/aceptar
    [HttpPost("invitacion/aceptar")]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AceptarInvitacion([FromBody] AceptarInvitacionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var usuario   = await _invitacionService.AceptarAsync(dto);
            var respuesta = _jwtService.GenerarRespuesta(usuario);
            return Ok(respuesta);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
