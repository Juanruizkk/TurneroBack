namespace TurneroApp.Application.DTOs.Auth;

/// <summary>
/// Resultado intermedio de la validación de credenciales, sin token JWT.
/// El token lo genera la capa Api (JwtService).
/// </summary>
public class UsuarioAutenticadoDto
{
    public string Rol { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool EsSuperAdmin { get; set; }
    public bool EsOwner { get; set; }
    public Guid? UserId { get; set; }
    public Guid? SucursalId { get; set; }
    public List<string> Permisos { get; set; } = [];
}
