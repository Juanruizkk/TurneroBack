namespace TurneroApp.Application.Exceptions;

/// <summary>
/// Se lanza cuando la suscripción de la sucursal está vencida, cancelada o en prueba expirada.
/// El controller debe retornar HTTP 402 Payment Required.
/// </summary>
public class SuscripcionVencidaException : Exception
{
    public SuscripcionVencidaException(string message) : base(message) { }
}
