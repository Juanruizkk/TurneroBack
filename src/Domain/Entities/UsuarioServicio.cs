namespace TurneroApp.Domain.Entities;

public class UsuarioServicio
{
    public Guid UsuarioSucursalRolId { get; set; }
    public Guid ServicioId { get; set; }
    public UsuarioSucursalRol UsuarioSucursalRol { get; set; } = null!;
    public Servicio Servicio { get; set; } = null!;
}
