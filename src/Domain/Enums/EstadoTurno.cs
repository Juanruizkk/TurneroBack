namespace TurneroApp.Domain.Enums;

public enum EstadoTurno
{
    Pendiente          = 0,
    Confirmado         = 1,
    CanceladoCliente   = 2,
    CanceladoStaff     = 3,
    Completado         = 4,
    NoShow             = 5,
    Reprogramado       = 6,
    PendientePago      = 7,  // turno Online con seña, esperando pago MP
    PagoRechazado      = 8   // el pago en MP fue rechazado o cancelado (distinto de cancelación voluntaria)
}
