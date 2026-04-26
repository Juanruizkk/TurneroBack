namespace TurneroApp.Application.DTOs.Cliente;

public class ClienteResponseDto
{
    public Guid Id { get; set; }
    public Guid SucursalId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Observaciones { get; set; }
    public string NombreCompleto => $"{Nombre} {Apellido}".Trim();
    public bool Activo { get; set; }
    public DateTime? FechaArchivado { get; set; }
    public bool EsFrecuente { get; set; }
    public bool TieneNoShow { get; set; }
    public int TotalTurnos { get; set; }
    public DateTime? UltimaVisita { get; set; }
}
