using TurneroApp.Application.DTOs.Servicio;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;

namespace TurneroApp.Application.Services;

public class ServicioService
{
    private readonly IServicioRepository _servicioRepository;

    public ServicioService(IServicioRepository servicioRepository)
    {
        _servicioRepository = servicioRepository;
    }

    public async Task<IEnumerable<ServicioResponseDto>> GetAllAsync()
    {
        var servicios = await _servicioRepository.GetAllAsync();
        return servicios.Select(MapToResponse);
    }

    public async Task<IEnumerable<ServicioResponseDto>> GetBySucursalAsync(Guid sucursalId)
    {
        var servicios = await _servicioRepository.GetBySucursalAsync(sucursalId);
        return servicios.Select(MapToResponse);
    }

    public async Task<ServicioResponseDto?> GetByIdAsync(Guid id)
    {
        var servicio = await _servicioRepository.GetByIdAsync(id);
        return servicio is null ? null : MapToResponse(servicio);
    }

    public async Task<ServicioResponseDto> CreateAsync(CreateServicioDto dto)
    {
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
        await _servicioRepository.SaveChangesAsync();
        return MapToResponse(servicio);
    }

    public async Task<ServicioResponseDto?> UpdateAsync(Guid id, UpdateServicioDto dto)
    {
        var servicio = await _servicioRepository.GetByIdAsync(id);
        if (servicio is null) return null;

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
        servicio.Activo = dto.Activo;

        _servicioRepository.Update(servicio);
        await _servicioRepository.SaveChangesAsync();
        return MapToResponse(servicio);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var servicio = await _servicioRepository.GetByIdAsync(id);
        if (servicio is null) return false;

        _servicioRepository.Delete(servicio);
        await _servicioRepository.SaveChangesAsync();
        return true;
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
        Activo = s.Activo
    };
}
