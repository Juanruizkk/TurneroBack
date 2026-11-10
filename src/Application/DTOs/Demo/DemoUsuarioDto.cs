namespace TurneroApp.Application.DTOs.Demo;

/// <summary>Un usuario demo con su JWT ya minteado, listo para elegir rol en el front.</summary>
public class DemoUsuarioDto
{
    /// <summary>"Owner" | "Administrativo" | "Profesional" | "Caja".</summary>
    public string Rol { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public bool EsOwner { get; set; }
    public string Token { get; set; } = string.Empty;
}
