namespace TurneroApp.Application.DTOs.Caja;

public class ResumenLiquidacionDto
{
    public DateTime FechaDesde    { get; set; }
    public DateTime FechaHasta    { get; set; }
    public decimal  TotalIngresos { get; set; }
    public decimal  TotalEgresos  { get; set; }
    public decimal  SaldoPeriodo  { get; set; }

    public List<LiquidacionProfesionalDto> LiquidacionesPorProfesional { get; set; } = [];
}
