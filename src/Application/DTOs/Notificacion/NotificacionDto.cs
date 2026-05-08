namespace TurneroApp.Application.DTOs.Notificacion;

public class NotificacionDto
{
    public string Id { get; set; } = string.Empty;

    /// <summary>"nuevo" | "cancelado" — mismo formato que espera el frontend.</summary>
    public string Tipo { get; set; } = string.Empty;

    public string Mensaje { get; set; } = string.Empty;

    public DateTime FechaCreacion { get; set; }

    public bool Leida { get; set; }
}
