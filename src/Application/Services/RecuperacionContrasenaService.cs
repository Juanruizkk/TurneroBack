using Microsoft.Extensions.Logging;
using TurneroApp.Application.Interfaces;

namespace TurneroApp.Application.Services;

public class RecuperacionContrasenaService
{
    private readonly IUsuarioRepository              _usuarioRepo;
    private readonly IEmailService                   _emailService;
    private readonly string                          _frontendUrl;
    private readonly ILogger<RecuperacionContrasenaService> _logger;

    public RecuperacionContrasenaService(
        IUsuarioRepository usuarioRepo,
        IEmailService      emailService,
        string             frontendUrl,
        ILogger<RecuperacionContrasenaService> logger)
    {
        _usuarioRepo  = usuarioRepo;
        _emailService = emailService;
        _frontendUrl  = frontendUrl;
        _logger       = logger;
    }

    /// <summary>
    /// Genera un token de recuperación y envía el email.
    /// Siempre responde sin revelar si el email existe o no.
    /// </summary>
    public async Task SolicitarAsync(string email)
    {
        var emailNorm = email.ToLowerInvariant().Trim();
        var usuario = await _usuarioRepo.GetByEmailGlobalAsync(emailNorm);

        if (usuario is null) return; // silencioso — no revelar si existe

        var token = Guid.NewGuid().ToString("N");

        usuario.TokenRecuperacion           = token;
        usuario.TokenRecuperacionExpiracion = DateTime.UtcNow.AddHours(1);

        _usuarioRepo.Update(usuario);
        await _usuarioRepo.SaveChangesAsync();

        // B-04: usar fragment (#) en lugar de query param (?token=...).
        // Los fragments nunca se envían al servidor en requests HTTP, no aparecen en
        // access logs ni en headers Referer hacia terceros (analytics, CDN).
        // El frontend lee: new URLSearchParams(window.location.hash.slice(1)).get('token')
        var link = $"{_frontendUrl.TrimEnd('/')}/reset-password#token={token}";
        try
        {
            await _emailService.EnviarRecuperacionContrasenaAsync(
                usuario.Email,
                $"{usuario.Nombre} {usuario.Apellido}".Trim(),
                link);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar email de recuperación de contraseña a {Email}", emailNorm);
        }
    }

    /// <summary>
    /// Valida el token y establece la nueva contraseña.
    /// Lanza <see cref="InvalidOperationException"/> si el token es inválido o expiró.
    /// </summary>
    public async Task RestablecerAsync(string token, string nuevaContrasena)
    {
        var usuario = await _usuarioRepo.GetByTokenRecuperacionAsync(token)
            ?? throw new InvalidOperationException("El enlace de recuperación no es válido.");

        if (usuario.TokenRecuperacionExpiracion is null ||
            usuario.TokenRecuperacionExpiracion.Value.ToUniversalTime() < DateTime.UtcNow)
            throw new InvalidOperationException(
                "El enlace de recuperación venció. Solicitá uno nuevo desde la pantalla de inicio de sesión.");

        usuario.PasswordHash                    = BCrypt.Net.BCrypt.HashPassword(nuevaContrasena);
        usuario.TokenRecuperacion               = null;
        usuario.TokenRecuperacionExpiracion     = null;

        _usuarioRepo.Update(usuario);
        await _usuarioRepo.SaveChangesAsync();
    }
}
