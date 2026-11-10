using System.ComponentModel.DataAnnotations;

namespace TurneroApp.Application.DTOs.Soporte;

/// <summary>Solicitud de ampliación de plan enviada por el owner desde su página de suscripción.</summary>
public class SolicitudAmpliacionDto
{
    /// <summary>Mensaje opcional del owner (por qué necesita más).</summary>
    [StringLength(1000)]
    public string? Mensaje { get; set; }

    [StringLength(200)]
    public string? SucursalNombre { get; set; }

    public int UsuariosActuales { get; set; }
    public int MaxUsuarios { get; set; }
    public int TurnosMesActual { get; set; }
    public int? MaxTurnosMes { get; set; }
}
