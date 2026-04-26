using System.ComponentModel.DataAnnotations;
using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.DTOs.Usuario;

public class UpdateRolEstadoPermisosDto
{
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
