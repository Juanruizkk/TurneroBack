namespace TurneroApp.Application.Exceptions;

/// <summary>
/// Se lanza cuando la funcionalidad solicitada no está incluida en el plan actual.
/// El controller debe retornar HTTP 403 Forbidden.
/// </summary>
public class FeatureNoPlanException : Exception
{
    public FeatureNoPlanException(string message) : base(message) { }
}
