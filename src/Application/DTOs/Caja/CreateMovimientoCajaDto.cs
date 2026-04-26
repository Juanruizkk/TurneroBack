using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.DTOs.Caja;

public class CreateMovimientoCajaDto
{
    public Guid CajaId { get; set; }
    public Guid? TurnoId { get; set; }
    public TipoMovimientoCaja TipoMovimiento { get; set; }
    public MedioPago MedioPago { get; set; }
    public decimal Monto { get; set; }
    public string? Observaciones { get; set; }
}
