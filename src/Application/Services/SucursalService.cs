using TurneroApp.Application.DTOs.Sucursal;
using TurneroApp.Application.Interfaces;
using TurneroApp.Application.Utilities;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.Services;

public class SucursalService
{
    /// <summary>Carpeta de Cloudinary donde se almacenan las imágenes de perfil de sucursales.</summary>
    private const string CarpetaImagenes = "TurneroApp/Sucursales";

    private readonly ISucursalRepository    _sucursalRepository;
    private readonly ICajaRepository        _cajaRepository;
    private readonly IAdjuntoService        _adjuntoService;
    private readonly ISuscripcionRepository _suscripcionRepository;

    public SucursalService(
        ISucursalRepository sucursalRepository,
        ICajaRepository cajaRepository,
        IAdjuntoService adjuntoService,
        ISuscripcionRepository suscripcionRepository)
    {
        _sucursalRepository    = sucursalRepository;
        _cajaRepository        = cajaRepository;
        _adjuntoService        = adjuntoService;
        _suscripcionRepository = suscripcionRepository;
    }

    public async Task<IEnumerable<SucursalResponseDto>> GetAllAsync()
    {
        var sucursales = await _sucursalRepository.GetAllAsync();
        return sucursales.Select(MapToResponse);
    }

    public async Task<IEnumerable<SucursalResponseDto>> GetActivasAsync()
    {
        var sucursales = await _sucursalRepository.GetActivasAsync();
        return sucursales.Select(MapToResponse);
    }

    public async Task<SucursalResponseDto?> GetByIdAsync(Guid id)
    {
        var sucursal = await _sucursalRepository.GetByIdAsync(id);
        return sucursal is null ? null : MapToResponse(sucursal);
    }

    public async Task<SucursalResponseDto> CreateAsync(CreateSucursalDto dto)
    {
        // Obtener plan base — obligatorio para iniciar trial
        var plan = await _suscripcionRepository.GetPlanBasicoAsync()
            ?? throw new InvalidOperationException("No existe un plan base activo. Creá al menos un plan antes de registrar sucursales.");

        if (plan.DuracionTrialDias <= 0)
            throw new InvalidOperationException("El plan base debe tener al menos 1 día de trial.");

        var slug = await SlugHelper.GenerateUniqueSlugAsync(
            dto.Nombre,
            async c => await _sucursalRepository.GetBySlugAsync(c) is not null);

        var ahora = DateTime.UtcNow;

        var sucursal = new Sucursal
        {
            Id            = Guid.NewGuid(),
            Nombre        = dto.Nombre,
            Slug          = slug,
            Direccion     = dto.Direccion,
            Telefono      = dto.Telefono,
            Email         = dto.Email,
            Activa        = true,
            FechaCreacion = ahora,
        };

        await _sucursalRepository.AddAsync(sucursal);

        // Toda sucursal nace con su caja
        await _cajaRepository.AddAsync(new Caja
        {
            Id         = Guid.NewGuid(),
            SucursalId = sucursal.Id,
            Nombre     = $"Caja — {dto.Nombre}",
            Activa     = true,
        });

        // Toda sucursal nace con una suscripción en período de prueba
        await _suscripcionRepository.AddAsync(new SucursalSuscripcion
        {
            Id               = Guid.NewGuid(),
            SucursalId       = sucursal.Id,
            PlanId           = plan.Id,
            Estado           = EstadoSuscripcion.PruebActiva,
            EnPeriodoPrueba  = true,
            FechaInicio      = ahora,
            FechaVencimiento = ahora.AddDays(plan.DuracionTrialDias),
        });

        await _sucursalRepository.SaveChangesAsync();
        return MapToResponse(sucursal);
    }

    public async Task<SucursalResponseDto?> UpdateAsync(Guid id, UpdateSucursalDto dto)
    {
        var sucursal = await _sucursalRepository.GetByIdAsync(id);
        if (sucursal is null) return null;

        sucursal.Nombre = dto.Nombre;
        sucursal.Direccion = dto.Direccion;
        sucursal.Telefono = dto.Telefono;
        sucursal.Email = dto.Email;
        sucursal.Activa = dto.Activa;

        _sucursalRepository.Update(sucursal);
        await _sucursalRepository.SaveChangesAsync();
        return MapToResponse(sucursal);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var sucursal = await _sucursalRepository.GetByIdAsync(id);
        if (sucursal is null) return false;

        _sucursalRepository.Delete(sucursal);
        await _sucursalRepository.SaveChangesAsync();
        return true;
    }

    // ─── Imágenes de perfil (logo y background) ───────────────────────────────

    /// <summary>
    /// Sube una imagen de logo o background a Cloudinary y la persiste en la sucursal.
    /// Si ya existía una imagen previa, la elimina de Cloudinary antes de subir la nueva.
    /// </summary>
    /// <param name="tipo">"logo" o "background"</param>
    public async Task<ImagenSucursalDto> SubirImagenAsync(
        Guid sucursalId,
        Stream stream,
        string fileName,
        string contentType,
        string tipo)
    {
        var esLogo = tipo.Equals("logo", StringComparison.OrdinalIgnoreCase);

        var sucursal = await _sucursalRepository.GetByIdAsync(sucursalId)
            ?? throw new InvalidOperationException($"Sucursal {sucursalId} no encontrada.");

        // Eliminar imagen anterior de Cloudinary si existe
        var publicIdAnterior = esLogo ? sucursal.FotoPublicId : sucursal.BackgroundPublicId;
        if (!string.IsNullOrEmpty(publicIdAnterior))
        {
            try { await _adjuntoService.EliminarArchivoAsync(publicIdAnterior, "image"); }
            catch { /* Si falla el borrado previo, continuamos igual */ }
        }

        // Subir nueva imagen
        var (url, publicId, _) = await _adjuntoService.SubirArchivoAsync(
            stream, fileName, contentType, CarpetaImagenes);

        // Persistir
        if (esLogo)
        {
            sucursal.FotoUrl       = url;
            sucursal.FotoPublicId  = publicId;
        }
        else
        {
            sucursal.BackgroundUrl       = url;
            sucursal.BackgroundPublicId  = publicId;
        }

        _sucursalRepository.Update(sucursal);
        await _sucursalRepository.SaveChangesAsync();

        return new ImagenSucursalDto { Url = url, PublicId = publicId, Tipo = tipo.ToLowerInvariant() };
    }

    /// <summary>
    /// Elimina la imagen de logo o background de Cloudinary y limpia los campos en la sucursal.
    /// </summary>
    /// <param name="tipo">"logo" o "background"</param>
    public async Task EliminarImagenAsync(Guid sucursalId, string tipo)
    {
        var esLogo = tipo.Equals("logo", StringComparison.OrdinalIgnoreCase);

        var sucursal = await _sucursalRepository.GetByIdAsync(sucursalId)
            ?? throw new InvalidOperationException($"Sucursal {sucursalId} no encontrada.");

        var publicId = esLogo ? sucursal.FotoPublicId : sucursal.BackgroundPublicId;

        if (!string.IsNullOrEmpty(publicId))
            await _adjuntoService.EliminarArchivoAsync(publicId, "image");

        if (esLogo)
        {
            sucursal.FotoUrl      = null;
            sucursal.FotoPublicId = null;
        }
        else
        {
            sucursal.BackgroundUrl      = null;
            sucursal.BackgroundPublicId = null;
        }

        _sucursalRepository.Update(sucursal);
        await _sucursalRepository.SaveChangesAsync();
    }

    // ─── Slug management ──────────────────────────────────────────────────────

    /// <summary>Devuelve el slug actual de la sucursal.</summary>
    public async Task<string> ObtenerSlugAsync(Guid sucursalId)
    {
        var sucursal = await _sucursalRepository.GetByIdAsync(sucursalId)
            ?? throw new InvalidOperationException($"Sucursal {sucursalId} no encontrada.");
        return sucursal.Slug;
    }

    /// <summary>
    /// Comprueba si un slug está disponible (válido + no en uso por otra sucursal).
    /// </summary>
    public async Task<bool> SlugDisponibleAsync(string slug, Guid? excluirSucursalId = null)
    {
        if (!SlugHelper.EsSlugValido(slug)) return false;
        return !await _sucursalRepository.SlugExisteAsync(slug, excluirSucursalId);
    }

    /// <summary>Actualiza el slug de una sucursal con todas las validaciones de negocio.</summary>
    public async Task<SucursalResponseDto> ActualizarSlugAsync(Guid sucursalId, string nuevoSlug)
    {
        var slug = nuevoSlug.Trim().ToLowerInvariant();

        if (!SlugHelper.EsSlugValido(slug))
            throw new InvalidOperationException("El slug es inválido. Usá solo letras, números y guiones (3–30 caracteres).");

        if (await _sucursalRepository.SlugExisteAsync(slug, sucursalId))
            throw new InvalidOperationException("El slug ya está en uso por otra sucursal.");

        var sucursal = await _sucursalRepository.GetByIdAsync(sucursalId)
            ?? throw new InvalidOperationException($"Sucursal {sucursalId} no encontrada.");

        sucursal.Slug = slug;
        _sucursalRepository.Update(sucursal);
        await _sucursalRepository.SaveChangesAsync();

        return MapToResponse(sucursal);
    }

    private static SucursalResponseDto MapToResponse(Sucursal s) => new()
    {
        Id        = s.Id,
        Nombre    = s.Nombre,
        Slug      = s.Slug,
        Direccion = s.Direccion,
        Telefono  = s.Telefono,
        Email     = s.Email,
        Activa    = s.Activa,
    };
}
