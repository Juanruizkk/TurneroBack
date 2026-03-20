using TurneroApp.Application.Interfaces;

namespace TurneroApp.Application.Services;

public class RecuperacionContrasenaService
{
    private readonly IUsuarioRepository _usuarioRepo;
    private readonly IEmailService      _emailService;
    private readonly string             _frontendUrl;

    public RecuperacionContrasenaService(
        IUsuarioRepository usuarioRepo,
        IEmailService      emailService,
        string             frontendUrl)
    {
        _usuarioRepo  = usuarioRepo;
        _emailService = emailService;
        _frontendUrl  = frontendUrl;
    }

    /// <summary>
    /// Genera un token de recuperación y envía el email.
    /// Siempre responde sin revelar si el email existe o no.
    /// </summary>
    public async Task SolicitarAsync(string email)
    {
        var emailNorm = email.ToLowerInvariant().Trim();
        var usuarios  = await _usuarioRepo.GetByEmailGlobalAsync(emailNorm);
        var usuario   = usuarios.FirstOrDefault();

        if (usuario is null) return; // silencioso — no revelar si existe

        var token = Guid.NewGuid().ToString("N");

        usuario.TokenRecuperacion           = token;
        usuario.TokenRecuperacionExpiracion = DateTime.UtcNow.AddHours(1);

        _usuarioRepo.Update(usuario);
        await _usuarioRepo.SaveChangesAsync();

        var link = $"{_frontendUrl.TrimEnd('/')}/reset-password?token={token}";
        try
        {
            await _emailService.EnviarRecuperacionContrasenaAsync(
                usuario.Email,
                $"{usuario.Nombre} {usuario.Apellido}".Trim(),
                link);
        }
        catch
        {
            // El email es no-crítico; el token quedó guardado.
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
            usuario.TokenRecuperacionExpiracion.Value < DateTime.UtcNow)
            throw new InvalidOperationException(
                "El enlace de recuperación venció. Solicitá uno nuevo desde la pantalla de inicio de sesión.");

        // TODO: reemplazar por BCrypt.HashPassword cuando se implemente hashing
        usuario.PasswordHash                    = nuevaContrasena;
        usuario.TokenRecuperacion               = null;
        usuario.TokenRecuperacionExpiracion     = null;

        _usuarioRepo.Update(usuario);
        await _usuarioRepo.SaveChangesAsync();
    }
}
