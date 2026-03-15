namespace TurneroApp.Domain.Enums;

public enum ModoConfirmacionTurnos
{
    /// <summary>El turno queda Confirmado automáticamente al ser creado por el cliente.</summary>
    Automatico = 0,

    /// <summary>El turno queda Pendiente y el staff debe confirmarlo manualmente.</summary>
    Manual = 1,
}
