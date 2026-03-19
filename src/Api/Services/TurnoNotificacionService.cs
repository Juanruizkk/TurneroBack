using Microsoft.AspNetCore.SignalR;
using TurneroApp.Api.Hubs;
using TurneroApp.Application.DTOs.Turno;
using TurneroApp.Application.Interfaces;

namespace TurneroApp.Api.Services;

public class TurnoNotificacionService : ITurnoNotificacionService
{
    private readonly IHubContext<TurneroHub> _hub;

    public TurnoNotificacionService(IHubContext<TurneroHub> hub)
    {
        _hub = hub;
    }

    public async Task NotificarNuevoTurnoAsync(TurnoResponseDto turno)
    {
        // Al profesional asignado
        await _hub.Clients.Group($"profesional-{turno.ProfesionalId}")
            .SendAsync("NuevoTurno", turno);

        // A todos los admins de la sucursal
        await _hub.Clients.Group($"admins-{turno.SucursalId}")
            .SendAsync("NuevoTurno", turno);
    }

    public async Task NotificarCancelacionAsync(TurnoResponseDto turno)
    {
        // Al profesional asignado
        await _hub.Clients.Group($"profesional-{turno.ProfesionalId}")
            .SendAsync("TurnoCancelado", turno);

        // A todos los admins de la sucursal
        await _hub.Clients.Group($"admins-{turno.SucursalId}")
            .SendAsync("TurnoCancelado", turno);
    }
}
