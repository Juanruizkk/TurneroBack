using System.ComponentModel.DataAnnotations;
using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.DTOs.Usuario;

public class UpdateUsuarioDto
{
    [Required]
    [MaxLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Apellido { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Telefono { get; set; } = string.Empty;

    [Required]
    public RolUsuario Rol { get; set; }

    [Required]
    public EstadoUsuario Estado { get; set; }

    /// <summary>
    /// Reemplaza completamente la lista de permisos del usuario.
    /// Enviar lista vacía para quitar todos los permisos.
    /// Los duplicados se ignoran.
    /// </summary>
    public List<PermisoSistema> Permisos { get; set; } = new();
}
