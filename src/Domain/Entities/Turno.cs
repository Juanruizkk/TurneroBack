using TurneroApp.Domain.Enums;

namespace TurneroApp.Domain.Entities;

public class Turno
{
    public Guid Id { get; set; }
    public Guid SucursalId { get; set; }
    public Guid ClienteId { get; set; }
    public Guid ServicioId { get; set; }
    public Guid ProfesionalId { get; set; }
    public DateTime FechaHoraInicio { get; set; }
    public DateTime FechaHoraFin { get; set; }
    public EstadoTurno Estado { get; set; } = EstadoTurno.Pendiente;
    public OrigenTurno Origen { get; set; }
    public string? Observaciones { get; set; }
    public string TokenGestion { get; set; } = string.Empty;
    public decimal PrecioServicio { get; set; }
    public decimal? MontoSena { get; set; }
    public decimal TotalCobrado { get; set; }

    public bool Recordatorio24hEnviado { get; set; } = false;
    public bool Recordatorio3hEnviado  { get; set; } = false;

    // Navigation properties
    public Sucursal Sucursal { get; set; } = null!;
    public Cliente Cliente { get; set; } = null!;
    public Servicio Servicio { get; set; } = null!;
    public Usuario Profesional { get; set; } = null!;
    public ICollection<ComentarioTurno> Comentarios { get; set; } = new List<ComentarioTurno>();
    public ICollection<AdjuntoTurno> Adjuntos { get; set; } = new List<AdjuntoTurno>();
    public ICollection<PagoTurno> Pagos { get; set; } = new List<PagoTurno>();
}
