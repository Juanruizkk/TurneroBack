namespace TurneroApp.Application.Interfaces;

/// <summary>
/// Contrato para el servicio de almacenamiento en la nube (Cloudinary).
/// La implementación vive en Infrastructure y no debe filtrarse a Application.
/// </summary>
public interface IAdjuntoService
{
    /// <summary>
    /// Sube un archivo a Cloudinary bajo la carpeta "TurneroApp/Adjuntos".
    /// </summary>
    /// <returns>
    /// Tupla con la URL segura, el PublicId y el tipo de recurso ("image" o "raw").
    /// </returns>
    Task<(string Url, string PublicId, string TipoRecurso)> SubirArchivoAsync(
        Stream stream,
        string fileName,
        string contentType);

    /// <summary>
    /// Borra físicamente un asset de Cloudinary usando su PublicId.
    /// </summary>
    Task EliminarArchivoAsync(string publicId, string tipoRecurso);
}
