namespace TurneroApp.Domain.Entities;

public class AdjuntoTurno
{
    public Guid Id { get; set; }
    public Guid TurnoId { get; set; }
    public Guid AutorUsuarioId { get; set; }

    /// <summary>URL segura de Cloudinary.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>PublicId de Cloudinary — necesario para el borrado físico.</summary>
    public string PublicId { get; set; } = string.Empty;

    /// <summary>Nombre original del archivo subido.</summary>
    public string NombreArchivo { get; set; } = string.Empty;

    /// <summary>"image" o "raw" (PDFs). Determina el resource_type en la API de Cloudinary.</summary>
    public string TipoRecurso { get; set; } = "image";

    public DateTime Fecha { get; set; }

    // Navigation properties
    public Turno Turno { get; set; } = null!;
    public Usuario Autor { get; set; } = null!;
}
