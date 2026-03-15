using TurneroApp.Domain.Enums;

namespace TurneroApp.Domain.Entities;

public class PagoTurno
{
    public Guid Id { get; set; }
    public Guid TurnoId { get; set; }
    public Guid MovimientoCajaId { get; set; }
    public ConceptoPagoTurno Concepto { get; set; }
    public decimal Monto { get; set; }
    public DateTime Fecha { get; set; }

    // Navigation properties
    public Turno Turno { get; set; } = null!;
    public MovimientoCaja MovimientoCaja { get; set; } = null!;
}
