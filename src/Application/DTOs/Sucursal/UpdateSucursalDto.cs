using System.ComponentModel.DataAnnotations;

namespace TurneroApp.Application.DTOs.Sucursal;

public class UpdateSucursalDto
{
    [Required]
    [MaxLength(150)]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    [MaxLength(300)]
    public string Direccion { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Telefono { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    public bool Activa { get; set; }
}
