using TurneroApp.Domain.Enums;

namespace TurneroApp.Domain.Entities;

public class Usuario
{
    public Guid Id { get; set; }
    public Guid SucursalId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public RolUsuario Rol { get; set; }
    public EstadoUsuario Estado { get; set; } = EstadoUsuario.PendienteInvitacion;
    public bool EsOwner { get; set; } = false;

    /// <summary>Token único para la invitación por email. Null cuando ya fue aceptada o no aplica.</summary>
    public string? InvitacionToken { get; set; }

    /// <summary>Fecha límite para aceptar la invitación.</summary>
    public DateTime? FechaExpiracionInvitacion { get; set; }

    /// <summary>Token de un solo uso para recuperación de contraseña.</summary>
    public string? TokenRecuperacion { get; set; }

    /// <summary>Fecha de expiración del token de recuperación (UTC).</summary>
    public DateTime? TokenRecuperacionExpiracion { get; set; }

    // Navigation properties
    public Sucursal Sucursal { get; set; } = null!;
    public ICollection<UsuarioPermiso> Permisos { get; set; } = new List<UsuarioPermiso>();
    public ICollection<HorarioLaboral> HorariosLaborales { get; set; } = new List<HorarioLaboral>();
    public ICollection<UsuarioServicio> Servicios { get; set; } = new List<UsuarioServicio>();
    public ICollection<MovimientoCaja> MovimientosRegistrados { get; set; } = new List<MovimientoCaja>();
    public ICollection<Turno> TurnosComoProfesional { get; set; } = new List<Turno>();
    public ICollection<ComentarioTurno> ComentariosRealizados { get; set; } = new List<ComentarioTurno>();
    public ICollection<AdjuntoTurno> AdjuntosSubidos { get; set; } = new List<AdjuntoTurno>();
}
