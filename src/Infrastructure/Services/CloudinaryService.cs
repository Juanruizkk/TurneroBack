using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;
using TurneroApp.Application.Interfaces;

namespace TurneroApp.Infrastructure.Services;

public class CloudinaryService : IAdjuntoService
{
    private const string FolderAdjuntos  = "TurneroApp/Adjuntos";
    private const string FolderSucursales = "TurneroApp/Sucursales";

    private readonly Cloudinary _cloudinary;

    public CloudinaryService(IConfiguration configuration)
    {
        var cloudName  = configuration["Cloudinary:CloudName"]  ?? throw new InvalidOperationException("Cloudinary:CloudName no configurado.");
        var apiKey     = configuration["Cloudinary:ApiKey"]     ?? throw new InvalidOperationException("Cloudinary:ApiKey no configurado.");
        var apiSecret  = configuration["Cloudinary:ApiSecret"]  ?? throw new InvalidOperationException("Cloudinary:ApiSecret no configurado.");

        var account  = new Account(cloudName, apiKey, apiSecret);
        _cloudinary  = new Cloudinary(account) { Api = { Secure = true } };
    }

    /// <inheritdoc />
    public async Task<(string Url, string PublicId, string TipoRecurso)> SubirArchivoAsync(
        Stream stream,
        string fileName,
        string contentType,
        string? carpeta = null)
    {
        var folder = carpeta ?? FolderAdjuntos;
        var esPdf  = contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);

        if (esPdf)
        {
            var uploadParams = new RawUploadParams
            {
                File           = new FileDescription(fileName, stream),
                Folder         = folder,
                UseFilename    = true,
                UniqueFilename = true,
            };

            var result = await _cloudinary.UploadAsync(uploadParams);

            if (result.Error is not null)
                throw new InvalidOperationException($"Error al subir archivo a Cloudinary: {result.Error.Message}");

            return (result.SecureUrl.ToString(), result.PublicId, "raw");
        }
        else
        {
            var uploadParams = new ImageUploadParams
            {
                File           = new FileDescription(fileName, stream),
                Folder         = folder,
                UseFilename    = true,
                UniqueFilename = true,
            };

            var result = await _cloudinary.UploadAsync(uploadParams);

            if (result.Error is not null)
                throw new InvalidOperationException($"Error al subir imagen a Cloudinary: {result.Error.Message}");

            return (result.SecureUrl.ToString(), result.PublicId, "image");
        }
    }


    /// <inheritdoc />
    public async Task EliminarArchivoAsync(string publicId, string tipoRecurso)
    {
        var resourceType = tipoRecurso.Equals("raw", StringComparison.OrdinalIgnoreCase)
            ? ResourceType.Raw
            : ResourceType.Image;

        var deleteParams = new DeletionParams(publicId)
        {
            ResourceType = resourceType
        };

        await _cloudinary.DestroyAsync(deleteParams);
    }
}
