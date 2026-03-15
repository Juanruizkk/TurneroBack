using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.DTOs.Turno;

public class UpdateTurnoDto
{
    public EstadoTurno Estado { get; set; }
    public string? Observaciones { get; set; }
    public DateTime? FechaHoraInicio { get; set; }
    public Guid? ProfesionalId { get; set; }
}
