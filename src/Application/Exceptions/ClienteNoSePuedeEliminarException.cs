namespace TurneroApp.Application.Exceptions;

public class ClienteNoSePuedeEliminarException : Exception
{
    public ClienteNoSePuedeEliminarException(string message) : base(message) { }
}
