using System.ComponentModel.DataAnnotations;

namespace TurneroApp.Application.DTOs.Turno;

public class CreateTurnoDto
{
    [Required]
    public Guid SucursalId { get; set; }

    [Required]
    public Guid ClienteId { get; set; }

    [Required]
    public Guid ServicioId { get; set; }

    [Required]
    public Guid ProfesionalId { get; set; }

    [Required]
    public DateTime FechaHoraInicio { get; set; }

    [MaxLength(500)]
    public string? Observaciones { get; set; }

    // Origen se asigna server-side según el contexto (autenticado = Administrativo, anónimo = Web)
}
