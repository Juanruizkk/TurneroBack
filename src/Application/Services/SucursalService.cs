using TurneroApp.Application.DTOs.Sucursal;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;

namespace TurneroApp.Application.Services;

public class SucursalService
{
    private readonly ISucursalRepository _sucursalRepository;
    private readonly ICajaRepository     _cajaRepository;

    public SucursalService(ISucursalRepository sucursalRepository, ICajaRepository cajaRepository)
    {
        _sucursalRepository = sucursalRepository;
        _cajaRepository     = cajaRepository;
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
        var sucursal = new Sucursal
        {
            Id        = Guid.NewGuid(),
            Nombre    = dto.Nombre,
            Direccion = dto.Direccion,
            Telefono  = dto.Telefono,
            Email     = dto.Email,
            Activa    = true,
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

    private static SucursalResponseDto MapToResponse(Sucursal s) => new()
    {
        Id = s.Id,
        Nombre = s.Nombre,
        Direccion = s.Direccion,
        Telefono = s.Telefono,
        Email = s.Email,
        Activa = s.Activa
    };
}
