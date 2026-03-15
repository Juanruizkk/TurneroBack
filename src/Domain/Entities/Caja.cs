namespace TurneroApp.Domain.Entities;

public class Caja
{
    public Guid Id { get; set; }
    public Guid SucursalId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool Activa { get; set; } = true;

    // Navigation properties
    public Sucursal Sucursal { get; set; } = null!;
    public ICollection<MovimientoCaja> Movimientos { get; set; } = new List<MovimientoCaja>();
}
