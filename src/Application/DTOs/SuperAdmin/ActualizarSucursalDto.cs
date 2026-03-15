using System.ComponentModel.DataAnnotations;

namespace TurneroApp.Application.DTOs.SuperAdmin;

public class ActualizarSucursalDto
{
    [Required]
    [MaxLength(200)]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Direccion { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Telefono { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;
}
