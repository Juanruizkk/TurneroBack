namespace TurneroApp.Domain.Entities;

public class UsuarioServicio
{
    public Guid UsuarioId  { get; set; }
    public Guid ServicioId { get; set; }
    public Usuario  Usuario  { get; set; } = null!;
    public Servicio Servicio { get; set; } = null!;
}
