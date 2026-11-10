using System.ComponentModel.DataAnnotations;
using TurneroApp.Application.Validation;

namespace TurneroApp.Application.DTOs.Auth;

public class ResetPasswordDto
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    [StrongPassword]
    public string NuevaContrasena { get; set; } = string.Empty;
}
