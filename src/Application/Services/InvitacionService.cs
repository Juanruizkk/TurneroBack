using TurneroApp.Application.DTOs.Auth;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.Services;

public class InvitacionService
{
    private readonly IUsuarioRepository _usuarioRepo;

    public InvitacionService(IUsuarioRepository usuarioRepo)
    {
        _usuarioRepo = usuarioRepo;
    }

    /// <summary>
    /// Valida el token, establece la contraseña y activa al usuario.
    /// Devuelve los datos del usuario para que la capa Api genere el JWT.
    /// </summary>
    public async Task<UsuarioAutenticadoDto> AceptarAsync(AceptarInvitacionDto dto)
    {
        var usuario = await _usuarioRepo.GetByInvitacionTokenAsync(dto.Token)
            ?? throw new InvalidOperationException("El enlace de invitación no es válido.");

        if (usuario.Estado != EstadoUsuario.PendienteInvitacion)
            throw new InvalidOperationException("Esta invitación ya fue utilizada.");

        if (usuario.FechaExpiracionInvitacion.HasValue &&
            usuario.FechaExpiracionInvitacion.Value < DateTime.UtcNow)
            throw new InvalidOperationException(
                "El enlace de invitación venció. Pedile al administrador que reenvíe la invitación.");

        // Activar usuario
        usuario.PasswordHash              = dto.NuevaPassword; // TODO: BCrypt.HashPassword
        usuario.Estado                    = EstadoUsuario.Activo;
        usuario.InvitacionToken           = null;
        usuario.FechaExpiracionInvitacion = null;

        _usuarioRepo.Update(usuario);
        await _usuarioRepo.SaveChangesAsync();

        return new UsuarioAutenticadoDto
        {
            Rol          = usuario.Rol.ToString(),
            Nombre       = usuario.Nombre,
            Apellido     = usuario.Apellido,
            Email        = usuario.Email,
            EsSuperAdmin = false,
            EsOwner      = usuario.EsOwner,
            UserId       = usuario.Id,
            SucursalId   = usuario.SucursalId,
        };
    }

    /// <summary>Devuelve info pública del token (nombre, sucursal) sin exponer datos sensibles.</summary>
    public async Task<InfoInvitacionDto> ObtenerInfoAsync(string token)
    {
        var usuario = await _usuarioRepo.GetByInvitacionTokenAsync(token)
            ?? throw new InvalidOperationException("El enlace de invitación no es válido.");

        if (usuario.Estado != EstadoUsuario.PendienteInvitacion)
            throw new InvalidOperationException("Esta invitación ya fue utilizada.");

        if (usuario.FechaExpiracionInvitacion.HasValue &&
            usuario.FechaExpiracionInvitacion.Value < DateTime.UtcNow)
            throw new InvalidOperationException("El enlace de invitación venció.");

        return new InfoInvitacionDto
        {
            Nombre        = usuario.Nombre,
            Apellido      = usuario.Apellido,
            Email         = usuario.Email,
            NombreSucursal = usuario.Sucursal?.Nombre ?? string.Empty,
            Rol            = usuario.Rol.ToString(),
        };
    }
}

public class InfoInvitacionDto
{
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string NombreSucursal { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
}
