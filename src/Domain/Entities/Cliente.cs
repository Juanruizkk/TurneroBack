namespace TurneroApp.Domain.Entities;

public class Cliente
{
    public Guid Id { get; set; }
    public Guid SucursalId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Observaciones { get; set; }

    // Archivado (soft delete)
    public bool Activo { get; set; } = true;
    public DateTime? FechaArchivado { get; set; }

    // Navigation properties
    public Sucursal Sucursal { get; set; } = null!;
    public ICollection<Turno> Turnos { get; set; } = new List<Turno>();

    // Métodos de dominio
    public void Archivar()
    {
        Activo = false;
        FechaArchivado = DateTime.UtcNow;
    }

    public void Desarchivar()
    {
        Activo = true;
        FechaArchivado = null;
    }
}
