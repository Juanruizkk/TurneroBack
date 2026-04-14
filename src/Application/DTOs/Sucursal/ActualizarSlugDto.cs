using System.ComponentModel.DataAnnotations;

namespace TurneroApp.Application.DTOs.Sucursal;

public class ActualizarSlugDto
{
    [Required]
    [MinLength(3)]
    [MaxLength(30)]
    [RegularExpression(@"^[a-z0-9][a-z0-9\-]*[a-z0-9]$|^[a-z0-9]$",
        ErrorMessage = "El slug solo puede contener letras minúsculas, números y guiones, sin empezar ni terminar con guión.")]
    public string Slug { get; set; } = string.Empty;
}
