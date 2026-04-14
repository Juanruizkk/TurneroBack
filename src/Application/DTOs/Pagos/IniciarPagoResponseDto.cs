namespace TurneroApp.Application.DTOs.Pagos;

public sealed class IniciarPagoResponseDto
{
    public string PreferenceId { get; init; } = string.Empty;

    /// <summary>
    /// URL de checkout lista para usar. En cuentas de prueba apunta a
    /// sandbox.mercadopago.com.ar; en producción a mercadopago.com.ar.
    /// El frontend debe redirigir aquí directamente.
    /// </summary>
    public string CheckoutUrl { get; init; } = string.Empty;
}
