using System.ComponentModel.DataAnnotations;

namespace TurneroApp.Application.DTOs.Auth;

public class SeleccionarSucursalDto
{
    [Required]
    public string SeleccionToken { get; set; } = string.Empty;

    [Required]
    public Guid SucursalId { get; set; }
}
