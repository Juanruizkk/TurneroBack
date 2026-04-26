namespace TurneroApp.Domain.Entities;

public class PagoSuscripcion
{
    public Guid     Id          { get; set; }
    public Guid     SucursalId  { get; set; }
    public Guid     PlanId      { get; set; }
    public string   PlanNombre  { get; set; } = string.Empty;
    public decimal  Monto       { get; set; }
    public long     MPPaymentId { get; set; }
    public DateTime FechaPago   { get; set; }

    // Navigation
    public Sucursal Sucursal { get; set; } = null!;
}
