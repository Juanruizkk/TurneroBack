using TurneroApp.Application.DTOs.Caja;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.Services;

public class CajaService
{
    private readonly ICajaRepository _cajaRepository;

    public CajaService(ICajaRepository cajaRepository)
    {
        _cajaRepository = cajaRepository;
    }

    public async Task<CajaResponseDto?> GetBySucursalAsync(Guid sucursalId)
    {
        var caja = await _cajaRepository.GetBySucursalAsync(sucursalId);
        return caja is null ? null : MapToResponse(caja);
    }

    public async Task<IEnumerable<MovimientoCajaResponseDto>> GetMovimientosAsync(
        Guid cajaId, DateTime? desde = null, DateTime? hasta = null)
    {
        var movimientos = await _cajaRepository.GetMovimientosByCajaAsync(cajaId, desde, hasta);
        return movimientos.Select(MapMovimientoToResponse);
    }

    public async Task<MovimientoCajaResponseDto> RegistrarMovimientoAsync(CreateMovimientoCajaDto dto)
    {
        var caja = await _cajaRepository.GetByIdAsync(dto.CajaId)
            ?? throw new InvalidOperationException($"Caja con Id {dto.CajaId} no encontrada.");

        var movimiento = new MovimientoCaja
        {
            Id = Guid.NewGuid(),
            CajaId = dto.CajaId,
            UsuarioId = dto.UsuarioId,
            TurnoId = dto.TurnoId,
            TipoMovimiento = dto.TipoMovimiento,
            MedioPago = dto.MedioPago,
            Monto = dto.Monto,
            Observaciones = dto.Observaciones,
            Fecha = DateTime.UtcNow
        };

        await _cajaRepository.AddMovimientoAsync(movimiento);
        await _cajaRepository.SaveChangesAsync();
        return MapMovimientoToResponse(movimiento);
    }

    private static CajaResponseDto MapToResponse(Caja c) => new()
    {
        Id = c.Id,
        SucursalId = c.SucursalId,
        Nombre = c.Nombre,
        Activa = c.Activa
    };

    private static MovimientoCajaResponseDto MapMovimientoToResponse(MovimientoCaja m) => new()
    {
        Id = m.Id,
        CajaId = m.CajaId,
        UsuarioId = m.UsuarioId,
        TurnoId = m.TurnoId,
        TipoMovimiento = m.TipoMovimiento,
        MedioPago = m.MedioPago,
        Monto = m.Monto,
        Observaciones = m.Observaciones,
        Fecha = m.Fecha
    };
}
