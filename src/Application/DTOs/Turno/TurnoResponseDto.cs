using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.DTOs.Turno;

public class TurnoResponseDto
{
    public Guid Id { get; set; }
    public Guid SucursalId { get; set; }
    public Guid ClienteId { get; set; }
    public string ClienteNombre { get; set; } = string.Empty;
    public string ClienteTelefono { get; set; } = string.Empty;
    public Guid ServicioId { get; set; }
    public string ServicioNombre { get; set; } = string.Empty;
    public Guid ProfesionalId { get; set; }
    public string ProfesionalNombre { get; set; } = string.Empty;
    public DateTime FechaHoraInicio { get; set; }
    public DateTime FechaHoraFin { get; set; }
    public EstadoTurno Estado { get; set; }
    public OrigenTurno Origen { get; set; }
    public string? Observaciones { get; set; }
    public string TokenGestion { get; set; } = string.Empty;
    public decimal PrecioServicio { get; set; }
    public decimal? MontoSena { get; set; }
    public decimal TotalCobrado { get; set; }
}
