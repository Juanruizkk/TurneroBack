namespace TurneroApp.Application.DTOs.Auth;

public class LoginResponseDto
{
    // ── Caso 1: login directo (1 sucursal) ──────────────────────────────────
    public string? Token { get; set; }

    /// <summary>"SuperAdmin" | "Administrativo" | "Profesional" | "Caja"</summary>
    public string? Rol { get; set; }

    public string? Nombre { get; set; }
    public string? Apellido { get; set; }
    public string? Email { get; set; }

    public bool EsSuperAdmin { get; set; }
    public bool EsOwner { get; set; }

    /// <summary>Null para SuperAdmin.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Null para SuperAdmin.</summary>
    public Guid? SucursalId { get; set; }
    public List<string>? Permisos { get; set; }

    // ── Caso 2: múltiples sucursales — requiere selección ───────────────────
    public bool RequiereSeleccion { get; set; } = false;
    public string? SeleccionToken { get; set; }
    public List<OpcionSucursalDto>? Opciones { get; set; }
}
