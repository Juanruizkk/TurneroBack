namespace TurneroApp.Application.DTOs.Turno;

public class ComentarioTurnoResponseDto
{
    public Guid   Id              { get; set; }
    public Guid   TurnoId         { get; set; }
    public Guid   AutorUsuarioId  { get; set; }
    public string AutorNombre     { get; set; } = string.Empty;
    public string Texto           { get; set; } = string.Empty;
    public DateTime Fecha         { get; set; }
}

public class CrearComentarioDto
{
    public string Texto { get; set; } = string.Empty;
}
