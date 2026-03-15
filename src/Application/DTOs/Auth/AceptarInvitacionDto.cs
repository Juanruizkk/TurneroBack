using System.ComponentModel.DataAnnotations;

namespace TurneroApp.Application.DTOs.Auth;

public class AceptarInvitacionDto
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    [MaxLength(200)]
    public string NuevaPassword { get; set; } = string.Empty;
}
