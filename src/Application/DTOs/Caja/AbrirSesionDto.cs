using System.ComponentModel.DataAnnotations;

namespace TurneroApp.Application.DTOs.Caja;

public class AbrirSesionDto
{
    [Required]
    [Range(0, 9_999_999.99)]
    public decimal MontoInicial  { get; set; }

    [MaxLength(500)]
    public string? Observaciones { get; set; }
}
