using System.ComponentModel.DataAnnotations;

namespace TurneroApp.Application.DTOs.SuperAdmin;

public class CambiarPrecioPlanDto
{
    [Range(0, 9999999)] public decimal PrecioMensual { get; set; }
}
