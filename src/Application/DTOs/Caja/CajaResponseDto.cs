namespace TurneroApp.Application.DTOs.Caja;

public class CajaResponseDto
{
    public Guid Id { get; set; }
    public Guid SucursalId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool Activa { get; set; }
}
