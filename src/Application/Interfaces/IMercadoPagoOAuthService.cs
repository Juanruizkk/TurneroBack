namespace TurneroApp.Application.Interfaces;

public record MercadoPagoEstadoDto(bool Conectado, string? MpUserId, DateTime? FechaConexionUtc);

public interface IMercadoPagoOAuthService
{
    /// <summary>Genera la URL de autorización a la que redirigir al usuario.</summary>
    string GenerarUrlAutorizacion(Guid sucursalId);

    /// <summary>Intercambia el code recibido en el callback por tokens y los persiste.</summary>
    Task ConectarAsync(Guid sucursalId, string code);

    /// <summary>Devuelve el access_token vigente, refrescándolo si está por expirar.</summary>
    Task<string> ObtenerAccessTokenVigenteAsync(Guid sucursalId);

    /// <summary>Refresca el access_token usando el refresh_token.</summary>
    Task RefrescarTokenAsync(Guid sucursalId);

    /// <summary>Devuelve el estado de conexión con Mercado Pago para una sucursal.</summary>
    Task<MercadoPagoEstadoDto> ObtenerEstadoAsync(Guid sucursalId);

    /// <summary>Desvincula la cuenta de Mercado Pago de una sucursal.</summary>
    Task DesconectarAsync(Guid sucursalId);
}
