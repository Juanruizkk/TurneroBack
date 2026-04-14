namespace TurneroApp.Domain.Enums;

public enum EstadoSuscripcion
{
    PruebActiva    = 0,  // período de prueba en curso
    Activa         = 1,  // suscripción paga activa
    Vencida        = 2,  // expiró sin renovar
    Cancelada      = 3,  // cancelada manualmente
    PruebVencida   = 4,  // período de prueba expiró sin convertir
}
