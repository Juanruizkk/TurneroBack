using TurneroApp.Domain.Enums;

namespace TurneroApp.Domain.Entities;

public class UsuarioSucursalRol
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public Guid SucursalId { get; set; }
    public RolUsuario Rol { get; set; }
    public bool EsOwner { get; set; }
    public EstadoUsuario Estado { get; set; }

    /// <summary>Token único para la invitación por email. Null cuando ya fue aceptada o no aplica.</summary>
    public string? InvitacionToken { get; set; }

    /// <summary>Fecha límite para aceptar la invitación.</summary>
    public DateTime? FechaExpiracionInvitacion { get; set; }

    public DateTime FechaCreacion { get; set; }

    // Navigation properties
    public Usuario Usuario { get; set; } = null!;
    public Sucursal Sucursal { get; set; } = null!;
    public ICollection<UsuarioPermiso> Permisos { get; set; } = [];
    public ICollection<HorarioLaboral> HorariosLaborales { get; set; } = [];
    public ICollection<UsuarioServicio> Servicios { get; set; } = [];
}
