namespace TurneroApp.Application.DTOs.Caja;

public class LiquidacionProfesionalDto
{
    public Guid   ProfesionalId           { get; set; }
    public string Nombre                  { get; set; } = string.Empty;
    public int    CantidadTurnosAtendidos { get; set; }
    public decimal TotalGenerado          { get; set; }
}
