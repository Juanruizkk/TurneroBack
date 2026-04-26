namespace TurneroApp.Application.DTOs.Pagos;

public sealed class PagoSuscripcionDto
{
    public Guid     Id          { get; init; }
    public string   PlanNombre  { get; init; } = string.Empty;
    public decimal  Monto       { get; init; }
    public long     MPPaymentId { get; init; }
    public DateTime FechaPago   { get; init; }
}
