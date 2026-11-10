namespace TurneroApp.Application.DTOs.Demo;

/// <summary>
/// Resultado de <see cref="Interfaces.IDemoTenantService.CrearAsync"/>: la sucursal demo
/// creada con sus usuarios (sin tokens — la capa Api mintea los JWT).
/// </summary>
public class DemoCreacionResultado
{
    public Guid SucursalId { get; set; }
    public DateTime ExpiraEn { get; set; }
    public List<DemoUsuarioCreado> Usuarios { get; set; } = [];
}

/// <summary>Datos de un usuario demo creado, usados por la Api para mintear su JWT.</summary>
public class DemoUsuarioCreado
{
    public Guid UsuarioId { get; set; }
    public Guid SucursalId { get; set; }
    public string Rol { get; set; } = string.Empty;       // RolUsuario.ToString()
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool EsOwner { get; set; }
    public List<string> Permisos { get; set; } = [];
}
