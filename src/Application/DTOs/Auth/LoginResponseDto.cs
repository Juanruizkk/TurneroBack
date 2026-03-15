namespace TurneroApp.Application.DTOs.Auth;

public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;

    /// <summary>"SuperAdmin" | "Administrativo" | "Profesional" | "Caja"</summary>
    public string Rol { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public bool EsSuperAdmin { get; set; }
    public bool EsOwner { get; set; }

    /// <summary>Null para SuperAdmin.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Null para SuperAdmin.</summary>
    public Guid? SucursalId { get; set; }
    public List<string> Permisos { get; set; } = [];
}
