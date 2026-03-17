namespace TurneroApp.Application.DTOs.Caja;

public class TurnoResumenCajaDto
{
    public Guid     Id               { get; set; }
    public string   ClienteNombre    { get; set; } = string.Empty;
    public string   ServicioNombre   { get; set; } = string.Empty;
    public string   ProfesionalNombre{ get; set; } = string.Empty;
    public DateTime FechaHoraInicio  { get; set; }
    public decimal  PrecioServicio   { get; set; }
    public decimal  TotalCobrado     { get; set; }
}
