using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.DTOs.Ajustes;

public class AjustesSucursalDto
{
    // ─── Identidad ────────────────────────────────────────────────────────────
    public string Nombre { get; set; } = string.Empty;
    public string? Slogan { get; set; }
    public string? FotoUrl { get; set; }
    public string? BackgroundUrl { get; set; }
    public List<string> Rubros { get; set; } = new();
    public string Moneda { get; set; } = "ARS";

    // ─── Contacto y ubicación ─────────────────────────────────────────────────
    public string Direccion { get; set; } = string.Empty;
    public string? GoogleMapsUrl { get; set; }
    public string Telefono { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    // ─── Horarios de atención ─────────────────────────────────────────────────
    /// <summary>Clave: nombre del día (ej: "Lunes"). Valor: horario del día.</summary>
    public Dictionary<string, HorarioDiaDto> HorariosAtencion { get; set; } = new();

    // ─── Config operativa de reservas ─────────────────────────────────────────
    public int MinutosAnticipacionMinimaReserva { get; set; }
    public int DiasMaximosReservaFutura { get; set; }
    public int MinutosBufferEntreTurnos { get; set; }
    public bool PermiteCancelacionCliente { get; set; }
    public int HorasMinimasCancelacion { get; set; }
    public bool EnviaRecordatorioEmail { get; set; }
    public bool EnviaRecordatorioWhatsapp { get; set; }
    public ModoConfirmacionTurnos ModoConfirmacionTurnos { get; set; }

    // ─── Plantillas de mensaje WhatsApp ───────────────────────────────────────
    /// <summary>Variables: {{ClienteNombre}}, {{Servicio}}, {{Fecha}}, {{Hora}}</summary>
    public string? MensajeWhatsApp24h { get; set; }
    /// <summary>Variables: {{ClienteNombre}}, {{Servicio}}, {{Fecha}}, {{Hora}}</summary>
    public string? MensajeWhatsApp3h { get; set; }
}
