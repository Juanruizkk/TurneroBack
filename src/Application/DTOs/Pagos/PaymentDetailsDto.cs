namespace TurneroApp.Application.DTOs.Pagos;

/// <summary>DTO interno que representa el resultado de un pago consultado a MP.</summary>
public sealed class PaymentDetailsDto
{
    public long    PaymentId         { get; init; }
    public string  Status            { get; init; } = string.Empty;
    public string  StatusDetail      { get; init; } = string.Empty;
    public string  ExternalReference { get; init; } = string.Empty;
    public decimal Amount            { get; init; }
}
