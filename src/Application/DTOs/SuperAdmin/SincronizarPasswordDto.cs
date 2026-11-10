using System.ComponentModel.DataAnnotations;
using TurneroApp.Application.Validation;

namespace TurneroApp.Application.DTOs.SuperAdmin;

public class SincronizarPasswordDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StrongPassword]
    public string NuevoPassword { get; set; } = string.Empty;
}
