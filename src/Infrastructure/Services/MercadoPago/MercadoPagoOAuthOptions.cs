namespace TurneroApp.Infrastructure.Services.MercadoPago;

public sealed class MercadoPagoOAuthOptions
{
    public const string SectionName = "MercadoPagoOAuth";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>URL base de autorización. Por defecto: https://auth.mercadopago.com/authorization</summary>
    public string AuthorizationBaseUrl { get; set; } = "https://auth.mercadopago.com/authorization";

    /// <summary>Endpoint para obtener/refrescar tokens. Por defecto: https://api.mercadolibre.com/oauth/token</summary>
    public string TokenUrl { get; set; } = "https://api.mercadolibre.com/oauth/token";

    /// <summary>
    /// Si true, agrega test_token=true al intercambio de tokens para obtener
    /// credenciales de sandbox. Solo activar en Development/testing.
    /// </summary>
    public bool TestMode { get; set; } = false;
}
