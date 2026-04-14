using System.ComponentModel.DataAnnotations;

namespace TurneroApp.Application.DTOs.SuperAdmin;

public class SincronizarPasswordDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string NuevoPassword { get; set; } = string.Empty;
}
