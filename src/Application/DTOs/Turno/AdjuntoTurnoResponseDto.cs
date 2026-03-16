namespace TurneroApp.Application.DTOs.Turno;

public class AdjuntoTurnoResponseDto
{
    public Guid     Id            { get; set; }
    public Guid     TurnoId       { get; set; }
    public string   Url           { get; set; } = string.Empty;
    public string   PublicId      { get; set; } = string.Empty;
    public string   NombreArchivo { get; set; } = string.Empty;
    public string   TipoRecurso   { get; set; } = "image";
    public DateTime Fecha         { get; set; }
}
