using TurneroApp.Domain.Enums;

namespace TurneroApp.Domain.Entities;

public class ConfiguracionSucursal
{
    public Guid Id { get; set; }
    public Guid SucursalId { get; set; }
    public int MinutosAnticipacionMinimaReserva { get; set; }
    public int DiasMaximosReservaFutura { get; set; }
    public int MinutosBufferEntreTurnos { get; set; }
    public bool PermiteCancelacionCliente { get; set; }
    public int HorasMinimasCancelacion { get; set; }
    public bool EnviaRecordatorioEmail { get; set; }
    public bool EnviaRecordatorioWhatsapp { get; set; }
    public ModoConfirmacionTurnos ModoConfirmacionTurnos { get; set; }

    /// <summary>
    /// Horarios de atención por día de la semana, almacenado como JSON.
    /// Formato: { "Lunes": { "abierto": true, "apertura": "09:00", "cierre": "18:00" }, ... }
    /// </summary>
    public string HorariosAtencionJson { get; set; } = "{}";

    // Navigation properties
    public Sucursal Sucursal { get; set; } = null!;
}
