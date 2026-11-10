namespace TurneroApp.Application.Exceptions;

/// <summary>Se excedió el rate-limit por IP o el tope global de demos activas.</summary>
public class DemoLimiteException(string message) : Exception(message);
