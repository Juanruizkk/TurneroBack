using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.DTOs.Turno;

/// <summary>
/// DTO mínimo para el endpoint público GET /api/turnos/by-token/{token}.
/// Excluye IDs internos, montos financieros y datos de origen.
/// </summary>
public class TurnoPublicoResponseDto
{
    public string ClienteNombre    { get; set; } = string.Empty;
    public string ServicioNombre   { get; set; } = string.Empty;
    public string ProfesionalNombre { get; set; } = string.Empty;
    public DateTime FechaHoraInicio { get; set; }
    public DateTime FechaHoraFin    { get; set; }
    public EstadoTurno Estado       { get; set; }
    public string? Observaciones    { get; set; }
}
