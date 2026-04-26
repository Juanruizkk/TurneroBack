namespace TurneroApp.Application.DTOs.Pagos;

public sealed class IniciarPagoSuscripcionRequestDto
{
    public Guid SucursalId { get; init; }
    public Guid PlanId     { get; init; }
}
