using TurneroApp.Domain.Enums;

namespace TurneroApp.Domain.Entities;

public class SucursalSuscripcion
{
    public Guid               Id               { get; set; }
    public Guid               SucursalId       { get; set; }
    public Guid               PlanId           { get; set; }
    public EstadoSuscripcion  Estado           { get; set; }
    public DateTime           FechaInicio      { get; set; }
    public DateTime           FechaVencimiento { get; set; }
    public bool               EnPeriodoPrueba  { get; set; }

    // Navigation properties
    public Sucursal Sucursal { get; set; } = null!;
    public Plan     Plan     { get; set; } = null!;
}
