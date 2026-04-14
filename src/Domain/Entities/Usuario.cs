namespace TurneroApp.Domain.Entities;

public class Usuario
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Token de un solo uso para recuperación de contraseña.</summary>
    public string? TokenRecuperacion { get; set; }

    /// <summary>Fecha de expiración del token de recuperación (UTC).</summary>
    public DateTime? TokenRecuperacionExpiracion { get; set; }

    /// <summary>Token temporal para el flujo de login multi-sucursal.</summary>
    public string? SeleccionToken { get; set; }

    /// <summary>Fecha de expiración del token de selección (UTC).</summary>
    public DateTime? SeleccionTokenExpiracion { get; set; }

    public DateTime FechaCreacion { get; set; }

    // Navigation properties
    public ICollection<UsuarioSucursalRol> Sucursales { get; set; } = new List<UsuarioSucursalRol>();
    public ICollection<MovimientoCaja> MovimientosRegistrados { get; set; } = new List<MovimientoCaja>();
    public ICollection<Turno> TurnosComoProfesional { get; set; } = new List<Turno>();
    public ICollection<ComentarioTurno> ComentariosRealizados { get; set; } = new List<ComentarioTurno>();
    public ICollection<AdjuntoTurno> AdjuntosSubidos { get; set; } = new List<AdjuntoTurno>();
}
