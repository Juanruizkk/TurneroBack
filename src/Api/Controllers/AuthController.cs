using Microsoft.AspNetCore.Mvc;
using TurneroApp.Application.DTOs.Auth;
using TurneroApp.Application.Services;
using TurneroApp.Api.Services;
using Microsoft.AspNetCore.Authorization;

namespace TurneroApp.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly AuthService                    _authService;
    private readonly JwtService                     _jwtService;
    private readonly InvitacionService              _invitacionService;
    private readonly RecuperacionContrasenaService  _recuperacionService;
    private readonly RegisterService                _registerService;

    public AuthController(
        AuthService authService,
        JwtService jwtService,
        InvitacionService invitacionService,
        RecuperacionContrasenaService recuperacionService,
        RegisterService registerService)
    {
        _authService         = authService;
        _jwtService          = jwtService;
        _invitacionService   = invitacionService;
        _recuperacionService = recuperacionService;
        _registerService     = registerService;
    }

    // POST /api/auth/register
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var usuario   = await _registerService.RegistrarAsync(dto);
            var respuesta = _jwtService.GenerarRespuesta(usuario);
            return StatusCode(StatusCodes.Status201Created, respuesta);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // POST /api/auth/login
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var validacion = await _authService.ValidarAsync(dto);

            if (validacion.RequiereSeleccion)
            {
                return Ok(new LoginResponseDto
                {
                    RequiereSeleccion = true,
                    SeleccionToken    = validacion.SeleccionToken,
                    Opciones          = validacion.Opciones,
                });
            }

            var respuesta = _jwtService.GenerarRespuesta(validacion.Usuario!);
            return Ok(respuesta);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    // POST /api/auth/seleccionar-sucursal
    /// <summary>
    /// Segundo paso del login multi-sucursal.
    /// Solo se llama cuando el login devuelve RequiereSeleccion = true.
    /// </summary>
    [HttpPost("seleccionar-sucursal")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SeleccionarSucursal([FromBody] SeleccionarSucursalDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var usuario   = await _authService.SeleccionarSucursalAsync(dto);
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

    // POST /api/auth/forgot-password
    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // Siempre 200 — no revelar si el email existe
        await _recuperacionService.SolicitarAsync(dto.Email);
        return Ok(new { message = "Si el email está registrado, recibirás un enlace en tu casilla." });
    }

    // POST /api/auth/reset-password
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            await _recuperacionService.RestablecerAsync(dto.Token, dto.NuevaContrasena);
            return NoContent();
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
