namespace TurneroApp.Infrastructure.Services.MercadoPago;

public sealed class MercadoPagoOptions
{
    public const string SectionName = "MercadoPago";

    /// <summary>Access token privado del vendedor. NUNCA exponer al frontend.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Clave pública para el SDK de frontend (MP Brick / botón).</summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>URL pública del webhook en este backend.</summary>
    public string NotificationUrl { get; set; } = string.Empty;
}
