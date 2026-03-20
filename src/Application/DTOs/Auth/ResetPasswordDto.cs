using System.ComponentModel.DataAnnotations;

namespace TurneroApp.Application.DTOs.Auth;

public class ResetPasswordDto
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string NuevaContrasena { get; set; } = string.Empty;
}
