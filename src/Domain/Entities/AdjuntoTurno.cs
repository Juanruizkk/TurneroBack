namespace TurneroApp.Domain.Entities;

public class AdjuntoTurno
{
    public Guid Id { get; set; }
    public Guid TurnoId { get; set; }
    public Guid AutorUsuarioId { get; set; }
    public string UrlArchivo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public DateTime Fecha { get; set; }

    // Navigation properties
    public Turno Turno { get; set; } = null!;
    public Usuario Autor { get; set; } = null!;
}
