using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.DTOs.Turno;

public class CreateTurnoDto
{
    public Guid SucursalId { get; set; }
    public Guid ClienteId { get; set; }
    public Guid ServicioId { get; set; }
    public Guid ProfesionalId { get; set; }
    public DateTime FechaHoraInicio { get; set; }
    public OrigenTurno Origen { get; set; }
    public string? Observaciones { get; set; }
}
