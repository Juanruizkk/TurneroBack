using TurneroApp.Domain.Enums;

namespace TurneroApp.Domain.Entities;

public class UsuarioPermiso
{
    public Guid Id { get; set; }
    public Guid UsuarioSucursalRolId { get; set; }
    public PermisoSistema Permiso { get; set; }

    // Navigation
    public UsuarioSucursalRol UsuarioSucursalRol { get; set; } = null!;
}
