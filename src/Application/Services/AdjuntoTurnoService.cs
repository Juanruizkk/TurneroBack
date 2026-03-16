using TurneroApp.Application.DTOs.Turno;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;

namespace TurneroApp.Application.Services;

public class AdjuntoTurnoService
{
    private readonly IAdjuntoTurnoRepository _adjuntoRepo;
    private readonly IAdjuntoService         _adjuntoService;

    public AdjuntoTurnoService(
        IAdjuntoTurnoRepository adjuntoRepo,
        IAdjuntoService adjuntoService)
    {
        _adjuntoRepo    = adjuntoRepo;
        _adjuntoService = adjuntoService;
    }

    public async Task<List<AdjuntoTurnoResponseDto>> ListarAsync(Guid turnoId)
    {
        var adjuntos = await _adjuntoRepo.GetByTurnoIdAsync(turnoId);
        return adjuntos.Select(MapToResponse).ToList();
    }

    public async Task<AdjuntoTurnoResponseDto> SubirAsync(
        Guid turnoId,
        Guid autorId,
        Stream stream,
        string fileName,
        string contentType)
    {
        var (url, publicId, tipoRecurso) = await _adjuntoService.SubirArchivoAsync(
            stream, fileName, contentType);

        var adjunto = new AdjuntoTurno
        {
            Id            = Guid.NewGuid(),
            TurnoId       = turnoId,
            AutorUsuarioId = autorId,
            Url           = url,
            PublicId      = publicId,
            NombreArchivo = fileName,
            TipoRecurso   = tipoRecurso,
            Fecha         = DateTime.UtcNow
        };

        await _adjuntoRepo.AddAsync(adjunto);
        await _adjuntoRepo.SaveChangesAsync();

        return MapToResponse(adjunto);
    }

    public async Task<bool> EliminarAsync(Guid adjuntoId)
    {
        var adjunto = await _adjuntoRepo.GetByIdAsync(adjuntoId);
        if (adjunto is null) return false;

        // Primero borrar de Cloudinary (si falla, no se modifica la DB)
        await _adjuntoService.EliminarArchivoAsync(adjunto.PublicId, adjunto.TipoRecurso);

        _adjuntoRepo.Delete(adjunto);
        await _adjuntoRepo.SaveChangesAsync();
        return true;
    }

    private static AdjuntoTurnoResponseDto MapToResponse(AdjuntoTurno a) => new()
    {
        Id            = a.Id,
        TurnoId       = a.TurnoId,
        Url           = a.Url,
        PublicId      = a.PublicId,
        NombreArchivo = a.NombreArchivo,
        TipoRecurso   = a.TipoRecurso,
        Fecha         = a.Fecha
    };
}
