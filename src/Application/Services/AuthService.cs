using TurneroApp.Application.DTOs.Auth;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.Services;

/// <summary>
/// Valida credenciales y devuelve un resultado sin token JWT.
/// La generación del token es responsabilidad de la capa Api (JwtService).
/// </summary>
public class AuthService
{
    private readonly IUsuarioRepository _usuarioRepo;

    // Credenciales del Super Admin inyectadas desde la capa Api
    // para no acoplar Application a IConfiguration.
    private readonly string _saEmail;
    private readonly string _saPassword;

    public AuthService(IUsuarioRepository usuarioRepo, string saEmail, string saPassword)
    {
        _usuarioRepo = usuarioRepo;
        _saEmail     = saEmail.ToLowerInvariant();
        _saPassword  = saPassword;
    }

    /// <summary>
    /// Valida las credenciales y devuelve el usuario autenticado.
    /// Lanza <see cref="UnauthorizedAccessException"/> si no coinciden.
    /// </summary>
    public async Task<UsuarioAutenticadoDto> ValidarAsync(LoginDto dto)
    {
        var emailNorm = dto.Email.ToLowerInvariant().Trim();

        // ── 1. Super Admin ────────────────────────────────────────────────────
        if (emailNorm == _saEmail && dto.Password == _saPassword)
        {
            return new UsuarioAutenticadoDto
            {
                Rol          = "SuperAdmin",
                Nombre       = "Super",
                Apellido     = "Admin",
                Email        = emailNorm,
                EsSuperAdmin = true,
                EsOwner      = false,
            };
        }

        // ── 2. Usuario registrado ─────────────────────────────────────────────
        var usuarios = await _usuarioRepo.GetByEmailGlobalAsync(emailNorm);

        // TODO: reemplazar comparación directa por BCrypt.Verify cuando se implemente hashing
        var usuario = usuarios.FirstOrDefault(u => u.PasswordHash == dto.Password);

        if (usuario is null)
            throw new UnauthorizedAccessException("Credenciales incorrectas.");

        if (usuario.Estado == EstadoUsuario.Inactivo)
            throw new UnauthorizedAccessException("El usuario está inactivo.");

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
            Permisos     = usuario.Permisos.Select(p => p.Permiso.ToString()).ToList(),
        };
    }
}
