using System.ComponentModel.DataAnnotations;

namespace TurneroApp.Application.DTOs.Cliente;

public class CreateClienteDto
{
    [Required]
    public Guid SucursalId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Apellido { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Telefono { get; set; } = string.Empty;

    [EmailAddress]
    [MaxLength(150)]
    public string? Email { get; set; }

    [MaxLength(500)]
    public string? Observaciones { get; set; }
}
