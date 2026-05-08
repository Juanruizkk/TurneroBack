using TurneroApp.Domain.Enums;

namespace TurneroApp.Domain.Entities;

public class Notificacion
{
    public Guid Id { get; set; }

    /// <summary>Sucursal a la que pertenece la notificación (multi-tenant).</summary>
    public Guid SucursalId { get; set; }

    /// <summary>Turno que generó esta notificación.</summary>
    public Guid TurnoId { get; set; }

    public TipoNotificacion Tipo { get; set; }

    /// <summary>Texto legible listo para mostrar en la campanita.</summary>
    public string Mensaje { get; set; } = string.Empty;

    /// <summary>True una vez que el usuario abre el panel de notificaciones.</summary>
    public bool Leida { get; set; }

    public DateTime FechaCreacion { get; set; }
}
