namespace TurneroApp.Application.DTOs.Sucursal;

public class ImagenSucursalDto
{
    /// <summary>URL segura de Cloudinary.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>PublicId de Cloudinary (para uso interno/debug).</summary>
    public string PublicId { get; set; } = string.Empty;

    /// <summary>"logo" o "background"</summary>
    public string Tipo { get; set; } = string.Empty;
}
