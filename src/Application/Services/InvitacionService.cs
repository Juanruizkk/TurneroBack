using TurneroApp.Application.DTOs.Auth;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.Services;

public class InvitacionService
{
    private readonly IUsuarioSucursalRolRepository _usrRepo;
    private readonly IUsuarioRepository _usuarioRepo;

    public InvitacionService(
        IUsuarioSucursalRolRepository usrRepo,
        IUsuarioRepository usuarioRepo)
    {
        _usrRepo     = usrRepo;
        _usuarioRepo = usuarioRepo;
    }

    /// <summary>
    /// Valida el token, establece la contraseña (si es usuario nuevo) y activa la membresía.
    /// Devuelve los datos del usuario para que la capa Api genere el JWT.
    /// </summary>
    public async Task<UsuarioAutenticadoDto> AceptarAsync(AceptarInvitacionDto dto)
    {
        var usr = await _usrRepo.GetByInvitacionTokenAsync(dto.Token)
            ?? throw new InvalidOperationException("El enlace de invitación no es válido.");

        if (usr.Estado != EstadoUsuario.PendienteInvitacion)
            throw new InvalidOperationException("Esta invitación ya fue utilizada.");

        if (usr.FechaExpiracionInvitacion.HasValue &&
            usr.FechaExpiracionInvitacion.Value < DateTime.UtcNow)
            throw new InvalidOperationException(
                "El enlace de invitación venció. Pedile al administrador que reenvíe la invitación.");

        var usuario = usr.Usuario;

        if (!string.IsNullOrWhiteSpace(dto.NuevaPassword))
        {
            // Usuario nuevo: establecer la contraseña
            usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NuevaPassword);
        }
        else
        {
            // Usuario existente: verificar que ya tiene contraseña
            if (string.IsNullOrEmpty(usuario.PasswordHash))
                throw new InvalidOperationException(
                    "Debés establecer una contraseña para activar tu cuenta.");
        }

        // Activar membresía
        usr.Estado                    = EstadoUsuario.Activo;
        usr.InvitacionToken           = null;
        usr.FechaExpiracionInvitacion = null;

        _usrRepo.Update(usr);
        await _usrRepo.SaveChangesAsync();

        return new UsuarioAutenticadoDto
        {
            Rol          = usr.Rol.ToString(),
            Nombre       = usuario.Nombre,
            Apellido     = usuario.Apellido,
            Email        = usuario.Email,
            EsSuperAdmin = false,
            EsOwner      = usr.EsOwner,
            UserId       = usuario.Id,
            SucursalId   = usr.SucursalId,
            Permisos     = usr.Permisos.Select(p => p.Permiso.ToString()).ToList(),
        };
    }

    /// <summary>Devuelve info pública del token (nombre, sucursal, yaRegistrado) sin exponer datos sensibles.</summary>
    public async Task<InfoInvitacionDto> ObtenerInfoAsync(string token)
    {
        var usr = await _usrRepo.GetByInvitacionTokenAsync(token)
            ?? throw new InvalidOperationException("El enlace de invitación no es válido.");

        if (usr.Estado != EstadoUsuario.PendienteInvitacion)
            throw new InvalidOperationException("Esta invitación ya fue utilizada.");

        if (usr.FechaExpiracionInvitacion.HasValue &&
            usr.FechaExpiracionInvitacion.Value < DateTime.UtcNow)
            throw new InvalidOperationException("El enlace de invitación venció.");

        return new InfoInvitacionDto
        {
            Nombre         = usr.Usuario.Nombre,
            Apellido       = usr.Usuario.Apellido,
            Email          = usr.Usuario.Email,
            NombreSucursal = usr.Sucursal?.Nombre ?? string.Empty,
            Rol            = usr.Rol.ToString(),
            YaRegistrado   = !string.IsNullOrEmpty(usr.Usuario.PasswordHash),
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
    public bool YaRegistrado { get; set; }
}
