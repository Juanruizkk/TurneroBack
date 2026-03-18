namespace TurneroApp.Application.DTOs.Pagos;

public sealed class IniciarPagoResponseDto
{
    /// <summary>
    /// ID de la preferencia MP. El frontend lo usa para renderizar
    /// el botón de pago con el SDK de MP.
    /// </summary>
    public string PreferenceId { get; init; } = string.Empty;
}
