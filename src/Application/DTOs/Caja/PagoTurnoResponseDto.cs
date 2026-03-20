using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.DTOs.Caja;

public class PagoTurnoResponseDto
{
    public Guid                Id       { get; set; }
    public ConceptoPagoTurno   Concepto { get; set; }
    public decimal             Monto    { get; set; }
    public DateTime            Fecha    { get; set; }
}
