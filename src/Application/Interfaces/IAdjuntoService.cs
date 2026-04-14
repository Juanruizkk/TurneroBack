namespace TurneroApp.Application.Interfaces;

/// <summary>
/// Contrato para el servicio de almacenamiento en la nube (Cloudinary).
/// La implementación vive en Infrastructure y no debe filtrarse a Application.
/// </summary>
public interface IAdjuntoService
{
    /// <summary>
    /// Sube un archivo a Cloudinary.
    /// </summary>
    /// <param name="stream">Stream del archivo.</param>
    /// <param name="fileName">Nombre original del archivo.</param>
    /// <param name="contentType">MIME type del archivo.</param>
    /// <param name="carpeta">
    /// Carpeta destino en Cloudinary. Si es <c>null</c> usa la carpeta por defecto
    /// "TurneroApp/Adjuntos".
    /// </param>
    /// <returns>
    /// Tupla con la URL segura, el PublicId y el tipo de recurso ("image" o "raw").
    /// </returns>
    Task<(string Url, string PublicId, string TipoRecurso)> SubirArchivoAsync(
        Stream stream,
        string fileName,
        string contentType,
        string? carpeta = null);

    /// <summary>
    /// Borra físicamente un asset de Cloudinary usando su PublicId.
    /// </summary>
    Task EliminarArchivoAsync(string publicId, string tipoRecurso);
}
