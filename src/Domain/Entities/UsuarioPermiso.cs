using TurneroApp.Domain.Enums;

namespace TurneroApp.Domain.Entities;

public class UsuarioPermiso
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public PermisoSistema Permiso { get; set; }

    // Navigation
    public Usuario Usuario { get; set; } = null!;
}
