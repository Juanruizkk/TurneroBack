using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.DTOs.Turno;

/// <summary>
/// DTO mínimo para el endpoint público GET /api/turnos/by-token/{token}.
/// Expone solo los datos necesarios para que el cliente gestione su turno.
/// </summary>
public class TurnoPublicoResponseDto
{
    public Guid     Id               { get; set; }
    public string   ClienteNombre    { get; set; } = string.Empty;
    public string   ServicioNombre   { get; set; } = string.Empty;
    public string   ProfesionalNombre { get; set; } = string.Empty;
    public DateTime FechaHoraInicio  { get; set; }
    public DateTime FechaHoraFin     { get; set; }
    public EstadoTurno Estado        { get; set; }
    public string?  Observaciones    { get; set; }
    public decimal  PrecioServicio   { get; set; }
    public decimal  TotalCobrado     { get; set; }
    public decimal? MontoSena        { get; set; }
}
