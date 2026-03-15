using System.ComponentModel.DataAnnotations;
using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.DTOs.Usuario;

public class CreateUsuarioDto
{
    [Required]
    public Guid SucursalId { get; set; }

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

    /// <summary>
    /// Lista de permisos a asignar al usuario.
    /// Cada valor debe ser un miembro válido de PermisoSistema.
    /// Los duplicados se ignoran.
    /// </summary>
    public List<PermisoSistema> Permisos { get; set; } = new();
}
