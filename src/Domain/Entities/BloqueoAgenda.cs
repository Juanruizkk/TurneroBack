namespace TurneroApp.Domain.Entities;

public class BloqueoAgenda
{
    public Guid Id { get; set; }
    public Guid SucursalId { get; set; }
    public Guid? UsuarioId { get; set; }
    public DateTime Desde { get; set; }
    public DateTime Hasta { get; set; }
    public string Motivo { get; set; } = string.Empty;

    // Navigation properties
    public Sucursal Sucursal { get; set; } = null!;
    public Usuario? Usuario { get; set; }
}
