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
    private readonly IUsuarioSucursalRolRepository _usrRepo;

    // Credenciales del Super Admin inyectadas desde la capa Api
    private readonly string _saEmail;
    private readonly string _saPassword;

    public AuthService(
        IUsuarioRepository usuarioRepo,
        IUsuarioSucursalRolRepository usrRepo,
        string saEmail,
        string saPassword)
    {
        _usuarioRepo = usuarioRepo;
        _usrRepo     = usrRepo;
        _saEmail     = saEmail.ToLowerInvariant();
        _saPassword  = saPassword;
    }

    /// <summary>
    /// Valida las credenciales y devuelve un <see cref="LoginValidacionDto"/>.
    /// Caso 1 sucursal: <c>Usuario</c> está poblado, <c>RequiereSeleccion</c> es false.
    /// Caso multi-sucursal: <c>RequiereSeleccion</c> es true y se devuelven las opciones con un token temporal.
    /// Lanza <see cref="UnauthorizedAccessException"/> si las credenciales no coinciden.
    /// </summary>
    public async Task<LoginValidacionDto> ValidarAsync(LoginDto dto)
    {
        var emailNorm = dto.Email.ToLowerInvariant().Trim();

        // ── 1. Super Admin ─────────────────────────────────────────────────────
        // Si el email coincide con el SA, el flujo termina aquí — nunca cae al lookup de usuario regular.
        if (emailNorm == _saEmail)
        {
            if (dto.Password != _saPassword)
                throw new UnauthorizedAccessException("Credenciales incorrectas.");

            return new LoginValidacionDto
            {
                Usuario = new UsuarioAutenticadoDto
                {
                    Rol          = "SuperAdmin",
                    Nombre       = "Super",
                    Apellido     = "Admin",
                    Email        = emailNorm,
                    EsSuperAdmin = true,
                    EsOwner      = false,
                }
            };
        }

        // ── 2. Buscar usuario global por email ────────────────────────────────
        var usuario = await _usuarioRepo.GetByEmailAsync(emailNorm);

        if (usuario is null)
            throw new UnauthorizedAccessException("Credenciales incorrectas.");

        bool passwordValida;
        try
        {
            passwordValida = BCrypt.Net.BCrypt.Verify(dto.Password, usuario.PasswordHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // El hash almacenado no tiene formato BCrypt válido (contraseña en texto plano legacy).
            // Se trata como credenciales inválidas — el usuario debe resetear su contraseña.
            passwordValida = false;
        }

        if (!passwordValida)
            throw new UnauthorizedAccessException("Credenciales incorrectas.");

        // ── 3. Obtener roles activos del usuario ──────────────────────────────
        var roles   = await _usrRepo.GetByUsuarioAsync(usuario.Id);
        var activos = roles.Where(r => r.Estado == EstadoUsuario.Activo).ToList();

        if (activos.Count == 0)
            throw new UnauthorizedAccessException("Tu cuenta no tiene sucursales activas.");

        // ── Caso 1: una sola sucursal activa → login directo ──────────────────
        if (activos.Count == 1)
        {
            return new LoginValidacionDto
            {
                Usuario = MapToAutenticado(usuario, activos.First())
            };
        }

        // ── Caso 2: múltiples sucursales → mostrar opciones con token temporal ─
        var token      = Guid.NewGuid().ToString("N");
        var expiracion = DateTime.UtcNow.AddMinutes(10);

        usuario.SeleccionToken           = token;
        usuario.SeleccionTokenExpiracion = expiracion;
        _usuarioRepo.Update(usuario);
        await _usuarioRepo.SaveChangesAsync();

        var opciones = activos.Select(r => new OpcionSucursalDto
        {
            UsuarioId      = usuario.Id,
            SucursalId     = r.SucursalId,
            SucursalNombre = r.Sucursal?.Nombre ?? "Sucursal",
            Rol            = r.Rol.ToString(),
            EsOwner        = r.EsOwner,
        }).ToList();

        return new LoginValidacionDto
        {
            RequiereSeleccion = true,
            SeleccionToken    = token,
            Opciones          = opciones,
        };
    }

    /// <summary>
    /// Segundo paso del login multi-sucursal.
    /// Valida el token temporal, lo limpia y devuelve el usuario seleccionado.
    /// Lanza <see cref="UnauthorizedAccessException"/> si el token es inválido o expiró.
    /// </summary>
    public async Task<UsuarioAutenticadoDto> SeleccionarSucursalAsync(SeleccionarSucursalDto dto)
    {
        var usuario = await _usuarioRepo.GetBySeleccionTokenAsync(dto.SeleccionToken)
            ?? throw new UnauthorizedAccessException("Token de selección inválido.");

        if (usuario.SeleccionTokenExpiracion == null || usuario.SeleccionTokenExpiracion < DateTime.UtcNow)
            throw new UnauthorizedAccessException("El token de selección expiró. Iniciá sesión nuevamente.");

        var usr = await _usrRepo.GetByUsuarioYSucursalAsync(usuario.Id, dto.SucursalId)
            ?? throw new UnauthorizedAccessException("Token de selección inválido.");

        // Limpiar token de selección
        usuario.SeleccionToken           = null;
        usuario.SeleccionTokenExpiracion = null;
        _usuarioRepo.Update(usuario);
        await _usuarioRepo.SaveChangesAsync();

        return MapToAutenticado(usuario, usr);
    }

    private static UsuarioAutenticadoDto MapToAutenticado(
        Domain.Entities.Usuario u,
        Domain.Entities.UsuarioSucursalRol usr) => new()
    {
        Rol          = usr.Rol.ToString(),
        Nombre       = u.Nombre,
        Apellido     = u.Apellido,
        Email        = u.Email,
        EsSuperAdmin = false,
        EsOwner      = usr.EsOwner,
        UserId       = u.Id,
        SucursalId   = usr.SucursalId,
        Permisos     = usr.Permisos.Select(p => p.Permiso.ToString()).ToList(),
    };
}
