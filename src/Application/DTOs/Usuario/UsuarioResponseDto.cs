using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.DTOs.Usuario;

public class UsuarioResponseDto
{
    public Guid Id { get; set; }
    public Guid SucursalId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string NombreCompleto => $"{Nombre} {Apellido}".Trim();
    public string Email { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public RolUsuario Rol { get; set; }
    public EstadoUsuario Estado { get; set; }

    /// <summary>
    /// Lista de permisos efectivos del usuario.
    /// </summary>
    public List<PermisoSistema> Permisos { get; set; } = new();

    /// <summary>Horarios laborales configurados. Vacío = sin restricción.</summary>
    public List<HorarioLaboralDto> HorariosLaborales { get; set; } = new();

    /// <summary>IDs de servicios que puede realizar. Vacío = todos los servicios.</summary>
    public List<Guid> ServiciosIds { get; set; } = new();
}
