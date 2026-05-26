using TurneroApp.Application.DTOs.Servicio;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.Services;

public class ServicioService
{
    private readonly IServicioRepository        _servicioRepository;
    private readonly IUsuarioServicioRepository _usuarioServicioRepo;
    private readonly SuscripcionGuard           _suscripcionGuard;

    public ServicioService(
        IServicioRepository        servicioRepository,
        IUsuarioServicioRepository usuarioServicioRepo,
        SuscripcionGuard           suscripcionGuard)
    {
        _servicioRepository  = servicioRepository;
        _usuarioServicioRepo = usuarioServicioRepo;
        _suscripcionGuard    = suscripcionGuard;
    }

    public async Task<IEnumerable<ServicioResponseDto>> GetAllAsync()
    {
        var servicios = await _servicioRepository.GetAllAsync();
        return servicios.Select(MapToResponse);
    }

    public async Task<IEnumerable<ServicioResponseDto>> GetBySucursalAsync(Guid sucursalId, bool incluirInactivos = false)
    {
        var servicios = await _servicioRepository.GetBySucursalAsync(sucursalId, incluirInactivos);
        return servicios.Select(MapToResponse);
    }

    public async Task<ServicioResponseDto?> GetByIdAsync(Guid id)
    {
        var servicio = await _servicioRepository.GetByIdConDiasAsync(id);
        return servicio is null ? null : MapToResponse(servicio);
    }

    public async Task<ServicioResponseDto> CreateAsync(CreateServicioDto dto)
    {
        await _suscripcionGuard.VerificarActivaAsync(dto.SucursalId);
        ValidarSena(dto.RequiereSena, dto.TipoSena, dto.ValorSena, dto.Precio);

        var servicio = new Servicio
        {
            Id = Guid.NewGuid(),
            SucursalId = dto.SucursalId,
            Nombre = dto.Nombre,
            Descripcion = dto.Descripcion,
            Aclaraciones = dto.Aclaraciones,
            Precio = dto.Precio,
            DuracionClienteMin = dto.DuracionClienteMin,
            DuracionAgendaMin = dto.DuracionAgendaMin,
            RequiereSena = dto.RequiereSena,
            TipoSena = dto.TipoSena,
            ValorSena = dto.ValorSena,
            ModalidadPrestacion = dto.ModalidadPrestacion,
            TipoServicio = dto.TipoServicio,
            Activo = true
        };

        await _servicioRepository.AddAsync(servicio);

        if (dto.DiasDisponibles is { Count: > 0 })
            await _servicioRepository.ActualizarDiasDisponiblesAsync(servicio.Id, dto.DiasDisponibles);

        // Asignar automáticamente a todos los profesionales activos de la sucursal
        await _usuarioServicioRepo.AsignarServicioASucursalAsync(dto.SucursalId, servicio.Id);

        await _servicioRepository.SaveChangesAsync();

        // Recargar con días para el response
        var creado = await _servicioRepository.GetByIdConDiasAsync(servicio.Id) ?? servicio;
        return MapToResponse(creado);
    }

    public async Task<ServicioResponseDto?> UpdateAsync(Guid id, UpdateServicioDto dto)
    {
        var servicio = await _servicioRepository.GetByIdConDiasAsync(id);
        if (servicio is null) return null;
        ValidarSena(dto.RequiereSena, dto.TipoSena, dto.ValorSena, dto.Precio);

        servicio.Nombre = dto.Nombre;
        servicio.Descripcion = dto.Descripcion;
        servicio.Aclaraciones = dto.Aclaraciones;
        servicio.Precio = dto.Precio;
        servicio.DuracionClienteMin = dto.DuracionClienteMin;
        servicio.DuracionAgendaMin = dto.DuracionAgendaMin;
        servicio.RequiereSena = dto.RequiereSena;
        servicio.TipoSena = dto.TipoSena;
        servicio.ValorSena = dto.ValorSena;
        servicio.ModalidadPrestacion = dto.ModalidadPrestacion;
        servicio.TipoServicio = dto.TipoServicio;
        // Activo no se modifica aquí — usar DELETE /api/servicios/{id}

        // Siempre reemplazar los días con la lista enviada (lista vacía = sin días = no disponible ningún día)
        await _servicioRepository.ActualizarDiasDisponiblesAsync(id, dto.DiasDisponibles ?? []);

        _servicioRepository.Update(servicio);
        await _servicioRepository.SaveChangesAsync();

        var actualizado = await _servicioRepository.GetByIdConDiasAsync(id) ?? servicio;
        return MapToResponse(actualizado);
    }

    public async Task<bool> DesactivarAsync(Guid id)
    {
        var servicio = await _servicioRepository.GetByIdAsync(id);
        if (servicio is null) return false;

        servicio.Activo = false;
        _servicioRepository.Update(servicio);
        await _servicioRepository.SaveChangesAsync();
        return true;
    }

    private static void ValidarSena(bool requiereSena, TipoSena? tipoSena, decimal? valorSena, decimal precio)
    {
        if (!requiereSena || tipoSena is null || valorSena is null) return;

        if (tipoSena == TipoSena.Fija && valorSena > precio)
            throw new InvalidOperationException(
                $"La seña fija (${valorSena:N2}) no puede ser mayor al precio del servicio (${precio:N2}).");

        if (tipoSena == TipoSena.Porcentaje && valorSena > 100)
            throw new InvalidOperationException(
                "El porcentaje de la seña no puede superar el 100%.");
    }

    private static ServicioResponseDto MapToResponse(Servicio s) => new()
    {
        Id = s.Id,
        SucursalId = s.SucursalId,
        Nombre = s.Nombre,
        Descripcion = s.Descripcion,
        Aclaraciones = s.Aclaraciones,
        Precio = s.Precio,
        DuracionClienteMin = s.DuracionClienteMin,
        DuracionAgendaMin = s.DuracionAgendaMin,
        RequiereSena = s.RequiereSena,
        TipoSena = s.TipoSena,
        ValorSena = s.ValorSena,
        ModalidadPrestacion = s.ModalidadPrestacion,
        TipoServicio = s.TipoServicio,
        Activo = s.Activo,
        DiasDisponibles = s.DiasDisponibles.Select(d => (int)d.DiaSemana).ToList(),
    };
}
