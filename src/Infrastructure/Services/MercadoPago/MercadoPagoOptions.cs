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

    /// <summary>
    /// Secreto del webhook de MP (campo "Secreto" en la configuración de notificaciones del panel de desarrolladores).
    /// Se usa para validar la firma HMAC-SHA256 del header x-signature.
    /// Si está vacío, la validación de firma se omite (modo desarrollo).
    /// </summary>
    public string WebhookSecret { get; set; } = string.Empty;

}
