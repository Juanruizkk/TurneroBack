using System.ComponentModel.DataAnnotations;
using TurneroApp.Application.Validation;

namespace TurneroApp.Application.DTOs.Auth;

public class AceptarInvitacionDto
{
    [Required]
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Contraseña para usuarios nuevos. Null para usuarios que ya tienen cuenta
    /// (en ese caso el <see cref="StrongPasswordAttribute"/> no aplica).
    /// </summary>
    [StrongPassword]
    [MaxLength(200)]
    public string? NuevaPassword { get; set; }
}
