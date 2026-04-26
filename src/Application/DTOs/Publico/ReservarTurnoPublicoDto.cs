using System.ComponentModel.DataAnnotations;

namespace TurneroApp.Application.DTOs.Publico;

public class ReservarTurnoPublicoDto
{
    [Required, MaxLength(100)]
    public string Nombre { get; set; } = "";

    [MaxLength(100)]
    public string? Apellido { get; set; }

    [Required, MaxLength(20)]
    public string Telefono { get; set; } = "";

    [MaxLength(100)]
    public string? Email { get; set; }

    [Required]
    public Guid ServicioId { get; set; }

    [Required]
    public Guid ProfesionalId { get; set; }

    [Required]
    public DateTime FechaHoraInicio { get; set; }

    [MaxLength(500)]
    public string? Observaciones { get; set; }
}
