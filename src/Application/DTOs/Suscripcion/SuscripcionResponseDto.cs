namespace TurneroApp.Application.DTOs.Suscripcion;

public class SuscripcionResponseDto
{
    public Guid     Id               { get; set; }
    public string   PlanNombre       { get; set; } = string.Empty;
    public decimal  PlanPrecio       { get; set; }
    public int      MaxUsuarios      { get; set; }
    public int?     MaxTurnosMes     { get; set; }
    public string   Estado           { get; set; } = string.Empty;
    public bool     EnPeriodoPrueba  { get; set; }
    public DateTime FechaVencimiento { get; set; }
    public int      DiasRestantes    { get; set; }

    /// <summary>Usuarios activos + pendientes que ocupan cupo.</summary>
    public int      UsuariosActuales  { get; set; }

    /// <summary>Turnos no cancelados en el mes en curso.</summary>
    public int      TurnosMesActual   { get; set; }

}
