namespace TurneroApp.Application.DTOs.Cliente;

public class ClienteFichaDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Observaciones { get; set; }
    public bool Activo { get; set; }
    public DateTime? FechaArchivado { get; set; }

    // Resumen
    public int TotalTurnos { get; set; }
    public int CantidadNoShow { get; set; }
    public int CantidadCancelaciones { get; set; }
    public DateTime? UltimoTurno { get; set; }
    public DateTime? ProximoTurno { get; set; }
    public decimal TotalCobrado { get; set; }
    public bool EsFrecuente { get; set; }
    public bool TieneNoShow { get; set; }

    // Historial de turnos pasados
    public List<ClienteTurnoHistorialDto> Historial { get; set; } = new();

    // Próximos turnos
    public List<ClienteProximoTurnoDto> ProximosTurnos { get; set; } = new();
}

public class ClienteTurnoHistorialDto
{
    public Guid TurnoId { get; set; }
    public DateTime Fecha { get; set; }
    public string Servicio { get; set; } = string.Empty;
    public string Profesional { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public decimal PrecioServicio { get; set; }
    public decimal TotalCobrado { get; set; }
    public bool TuvoSena { get; set; }
    public string? TokenGestion { get; set; }
}

public class ClienteProximoTurnoDto
{
    public Guid TurnoId { get; set; }
    public DateTime Fecha { get; set; }
    public string Servicio { get; set; } = string.Empty;
    public string Profesional { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
}
