using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.DTOs.Usuario;

public class UsuarioResponseDto
{
    /// <summary>UsuarioSucursalRol.Id — usar para endpoints de gestión de usuarios.</summary>
    public Guid Id { get; set; }
    /// <summary>Usuario.Id — usar como ProfesionalId al crear turnos y filtrar slots.</summary>
    public Guid UsuarioId { get; set; }
    public Guid SucursalId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string NombreCompleto => $"{Nombre} {Apellido}".Trim();
    public string Email { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public RolUsuario Rol { get; set; }
    public EstadoUsuario Estado { get; set; }
    public bool EsOwner { get; set; }

    /// <summary>
    /// Lista de permisos efectivos del usuario.
    /// </summary>
    public List<PermisoSistema> Permisos { get; set; } = new();

    /// <summary>Horarios laborales del profesional. Se inicializan con los días abiertos de la sucursal y se pueden ajustar individualmente.</summary>
    public List<HorarioLaboralDto> HorariosLaborales { get; set; } = new();

    /// <summary>IDs de servicios que puede realizar. Al crear un servicio nuevo se asigna automáticamente a todos los profesionales activos.</summary>
    public List<Guid> ServiciosIds { get; set; } = new();

    /// <summary>
    /// Link de invitación generado al crear o reenviar la invitación.
    /// Solo presente en la respuesta de creación o reenvío, no en listados.
    /// </summary>
    public string? InvitacionLink { get; set; }
}
