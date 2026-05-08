using TurneroApp.Domain.Enums;

namespace TurneroApp.Domain.Entities;

public class Sucursal
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Slogan { get; set; }
    public string? FotoUrl { get; set; }
    public string? FotoPublicId { get; set; }
    public string? BackgroundUrl { get; set; }
    public string? BackgroundPublicId { get; set; }
    public List<string> Rubros { get; set; } = new();
    public string Moneda { get; set; } = "ARS";
    public string Direccion { get; set; } = string.Empty;
    public string? GoogleMapsUrl { get; set; }
    public string Telefono { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool Activa { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<UsuarioSucursalRol> UsuarioSucursalRoles { get; set; } = new List<UsuarioSucursalRol>();
    public ICollection<Servicio> Servicios { get; set; } = new List<Servicio>();
    public ICollection<Cliente> Clientes { get; set; } = new List<Cliente>();
    public ICollection<Turno> Turnos { get; set; } = new List<Turno>();
    public Caja? Caja { get; set; }
    public ICollection<BloqueoAgenda> BloqueoAgendas { get; set; } = new List<BloqueoAgenda>();
    public ConfiguracionSucursal? ConfiguracionSucursal { get; set; }
}
