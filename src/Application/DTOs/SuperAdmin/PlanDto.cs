namespace TurneroApp.Application.DTOs.SuperAdmin;

public class PlanDto
{
    public Guid    Id            { get; set; }
    public string  Nombre        { get; set; } = string.Empty;
    public string? Descripcion   { get; set; }
    public int     MaxUsuarios   { get; set; }
    public int?    MaxTurnosMes  { get; set; }
    public decimal PrecioMensual { get; set; }
    public bool    EsActivo          { get; set; }
    public int     OrdenDisplay      { get; set; }
    public int     DuracionTrialDias { get; set; }
    public bool    TieneReportes     { get; set; }
}
