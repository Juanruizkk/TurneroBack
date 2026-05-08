using System.ComponentModel.DataAnnotations;

namespace TurneroApp.Application.DTOs.Auth;

public class AceptarInvitacionDto
{
    [Required]
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Contraseña para usuarios nuevos. Null para usuarios que ya tienen cuenta.
    /// </summary>
    [MinLength(8)]
    [MaxLength(200)]
    public string? NuevaPassword { get; set; }
}
