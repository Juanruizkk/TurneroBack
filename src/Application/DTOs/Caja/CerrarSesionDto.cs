using System.ComponentModel.DataAnnotations;

namespace TurneroApp.Application.DTOs.Caja;

public class CerrarSesionDto
{
    [Required]
    [Range(0, 9_999_999.99)]
    public decimal MontoFinalReal { get; set; }

    [MaxLength(500)]
    public string? Observaciones  { get; set; }
}
