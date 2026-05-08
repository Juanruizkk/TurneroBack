using System.ComponentModel.DataAnnotations;
using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.DTOs.Ajustes;

public class ActualizarAjustesDto
{
    // ─── Identidad ────────────────────────────────────────────────────────────
    [Required, MaxLength(200)]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Slogan { get; set; }

    [MaxLength(500)]
    public string? FotoUrl { get; set; }

    public List<string> Rubros { get; set; } = new();

    [Required, MaxLength(10)]
    public string Moneda { get; set; } = "ARS";

    // ─── Contacto y ubicación ─────────────────────────────────────────────────
    [Required(AllowEmptyStrings = true), MaxLength(500)]
    public string Direccion { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? GoogleMapsUrl { get; set; }

    [Required(AllowEmptyStrings = true), MaxLength(50)]
    public string Telefono { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = true), MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    // ─── Horarios de atención ─────────────────────────────────────────────────
    public Dictionary<string, HorarioDiaDto> HorariosAtencion { get; set; } = new();

    // ─── Config operativa de reservas ─────────────────────────────────────────
    [Range(0, 10080)]
    public int MinutosAnticipacionMinimaReserva { get; set; }

    [Range(1, 365)]
    public int DiasMaximosReservaFutura { get; set; }

    [Range(0, 120)]
    public int MinutosBufferEntreTurnos { get; set; }

    public bool PermiteCancelacionCliente { get; set; }

    [Range(0, 720)]
    public int HorasMinimasCancelacion { get; set; }

    public bool EnviaRecordatorioEmail { get; set; }
    public bool EnviaRecordatorioWhatsapp { get; set; }
    public ModoConfirmacionTurnos ModoConfirmacionTurnos { get; set; }

    // ─── Plantillas de mensaje WhatsApp ───────────────────────────────────────
    [MaxLength(2000)]
    public string? MensajeWhatsApp24h { get; set; }

    [MaxLength(2000)]
    public string? MensajeWhatsApp3h { get; set; }
}
