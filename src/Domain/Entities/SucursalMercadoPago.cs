namespace TurneroApp.Domain.Entities;

public class SucursalMercadoPago
{
    public Guid Id { get; set; }
    public Guid SucursalId { get; set; }

    public string MpUserId { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;

    public DateTime AccessTokenExpiraEnUtc { get; set; }
    public DateTime RefreshTokenExpiraEnUtc { get; set; }

    public DateTime FechaConexionUtc { get; set; }
    public DateTime? UltimaActualizacionUtc { get; set; }

    public bool Activo { get; set; } = true;

    // Navigation
    public Sucursal Sucursal { get; set; } = null!;
}
