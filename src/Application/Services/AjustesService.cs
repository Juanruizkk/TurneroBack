using System.Text.Json;
using TurneroApp.Application.DTOs.Ajustes;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;

namespace TurneroApp.Application.Services;

public class AjustesService
{
    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // Horarios por defecto: Lun-Vie 09-13 y 15-19, Sáb 09-13, Dom cerrado
    public static readonly Dictionary<string, HorarioDiaDto> HorariosDefault = new()
    {
        ["Lunes"]     = new() { Abierto = true,  Franjas = [new() { Apertura = "09:00", Cierre = "13:00" }, new() { Apertura = "15:00", Cierre = "19:00" }] },
        ["Martes"]    = new() { Abierto = true,  Franjas = [new() { Apertura = "09:00", Cierre = "13:00" }, new() { Apertura = "15:00", Cierre = "19:00" }] },
        ["Miercoles"] = new() { Abierto = true,  Franjas = [new() { Apertura = "09:00", Cierre = "13:00" }, new() { Apertura = "15:00", Cierre = "19:00" }] },
        ["Jueves"]    = new() { Abierto = true,  Franjas = [new() { Apertura = "09:00", Cierre = "13:00" }, new() { Apertura = "15:00", Cierre = "19:00" }] },
        ["Viernes"]   = new() { Abierto = true,  Franjas = [new() { Apertura = "09:00", Cierre = "13:00" }, new() { Apertura = "15:00", Cierre = "19:00" }] },
        ["Sabado"]    = new() { Abierto = true,  Franjas = [new() { Apertura = "09:00", Cierre = "13:00" }] },
        ["Domingo"]   = new() { Abierto = false, Franjas = [] },
    };

    private static readonly string _horariosDefault = JsonSerializer.Serialize(HorariosDefault);

    private readonly ISucursalRepository _sucursalRepo;
    private readonly IGenericRepository<ConfiguracionSucursal> _configRepo;

    public AjustesService(
        ISucursalRepository sucursalRepo,
        IGenericRepository<ConfiguracionSucursal> configRepo)
    {
        _sucursalRepo = sucursalRepo;
        _configRepo   = configRepo;
    }

    public async Task<AjustesSucursalDto> ObtenerAsync(Guid sucursalId)
    {
        var sucursal = await _sucursalRepo.GetByIdConConfiguracionAsync(sucursalId)
            ?? throw new InvalidOperationException("Sucursal no encontrada.");

        var config = sucursal.ConfiguracionSucursal;

        var horariosJson = config?.HorariosAtencionJson is { Length: > 2 }
            ? config.HorariosAtencionJson
            : _horariosDefault;

        var horarios = JsonSerializer.Deserialize<Dictionary<string, HorarioDiaDto>>(
            horariosJson, _jsonOpts) ?? new();

        return new AjustesSucursalDto
        {
            Nombre        = sucursal.Nombre,
            Slogan        = sucursal.Slogan,
            FotoUrl       = sucursal.FotoUrl,
            BackgroundUrl = sucursal.BackgroundUrl,
            Rubros        = sucursal.Rubros,
            Moneda       = sucursal.Moneda,
            Direccion    = sucursal.Direccion,
            GoogleMapsUrl = sucursal.GoogleMapsUrl,
            Telefono     = sucursal.Telefono,
            Email     = sucursal.Email,

            HorariosAtencion = horarios,

            MinutosAnticipacionMinimaReserva = config?.MinutosAnticipacionMinimaReserva ?? 60,
            DiasMaximosReservaFutura         = config?.DiasMaximosReservaFutura ?? 30,
            MinutosBufferEntreTurnos         = config?.MinutosBufferEntreTurnos ?? 0,
            PermiteCancelacionCliente        = config?.PermiteCancelacionCliente ?? true,
            HorasMinimasCancelacion          = config?.HorasMinimasCancelacion ?? 24,
            EnviaRecordatorioEmail           = config?.EnviaRecordatorioEmail ?? false,
            EnviaRecordatorioWhatsapp        = config?.EnviaRecordatorioWhatsapp ?? false,
            ModoConfirmacionTurnos           = config?.ModoConfirmacionTurnos ?? Domain.Enums.ModoConfirmacionTurnos.Automatico,
            MensajeWhatsApp24h               = config?.MensajeWhatsApp24h,
            MensajeWhatsApp3h                = config?.MensajeWhatsApp3h,
        };
    }

    public async Task<AjustesSucursalDto> ActualizarAsync(Guid sucursalId, ActualizarAjustesDto dto)
    {
        var sucursal = await _sucursalRepo.GetByIdConConfiguracionAsync(sucursalId)
            ?? throw new InvalidOperationException("Sucursal no encontrada.");

        // Actualizar campos de Sucursal
        sucursal.Nombre    = dto.Nombre.Trim();
        sucursal.Slogan        = string.IsNullOrWhiteSpace(dto.Slogan) ? null : dto.Slogan.Trim();
        sucursal.FotoUrl       = string.IsNullOrWhiteSpace(dto.FotoUrl) ? null : dto.FotoUrl.Trim();
        sucursal.BackgroundUrl = string.IsNullOrWhiteSpace(dto.BackgroundUrl) ? null : dto.BackgroundUrl.Trim();
        sucursal.Rubros    = dto.Rubros;
        sucursal.Moneda       = dto.Moneda;
        sucursal.Direccion    = dto.Direccion.Trim();
        sucursal.GoogleMapsUrl = string.IsNullOrWhiteSpace(dto.GoogleMapsUrl) ? null : dto.GoogleMapsUrl.Trim();
        sucursal.Telefono     = dto.Telefono.Trim();
        sucursal.Email     = dto.Email.Trim().ToLowerInvariant();

        _sucursalRepo.Update(sucursal);

        // Actualizar o crear ConfiguracionSucursal
        var config = sucursal.ConfiguracionSucursal;
        if (config is null)
        {
            config = new ConfiguracionSucursal
            {
                Id         = Guid.NewGuid(),
                SucursalId = sucursalId,
            };
            await _configRepo.AddAsync(config);
        }

        config.MinutosAnticipacionMinimaReserva = dto.MinutosAnticipacionMinimaReserva;
        config.DiasMaximosReservaFutura         = dto.DiasMaximosReservaFutura;
        config.MinutosBufferEntreTurnos         = dto.MinutosBufferEntreTurnos;
        config.PermiteCancelacionCliente        = dto.PermiteCancelacionCliente;
        config.HorasMinimasCancelacion          = dto.HorasMinimasCancelacion;
        config.EnviaRecordatorioEmail           = dto.EnviaRecordatorioEmail;
        config.EnviaRecordatorioWhatsapp        = dto.EnviaRecordatorioWhatsapp;
        config.ModoConfirmacionTurnos           = dto.ModoConfirmacionTurnos;
        config.HorariosAtencionJson             = JsonSerializer.Serialize(dto.HorariosAtencion);
        config.MensajeWhatsApp24h               = string.IsNullOrWhiteSpace(dto.MensajeWhatsApp24h) ? null : dto.MensajeWhatsApp24h.Trim();
        config.MensajeWhatsApp3h                = string.IsNullOrWhiteSpace(dto.MensajeWhatsApp3h)  ? null : dto.MensajeWhatsApp3h.Trim();

        await _sucursalRepo.SaveChangesAsync();

        return await ObtenerAsync(sucursalId);
    }
}
