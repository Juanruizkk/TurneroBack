namespace TurneroApp.Domain.Entities;

public class ComentarioTurno
{
    public Guid Id { get; set; }
    public Guid TurnoId { get; set; }
    public Guid AutorUsuarioId { get; set; }
    public string Texto { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }

    // Navigation properties
    public Turno Turno { get; set; } = null!;
    public Usuario Autor { get; set; } = null!;
}
