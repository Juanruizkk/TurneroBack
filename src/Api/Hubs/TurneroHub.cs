using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace TurneroApp.Api.Hubs;

[Authorize]
public class TurneroHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        if (user is null) { await base.OnConnectedAsync(); return; }

        var rol          = user.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        var userId       = user.FindFirstValue("userId");
        var sucursalId   = user.FindFirstValue("sucursalId");
        var esSuperAdmin = user.FindFirstValue("esSuperAdmin") == "true";

        if (esSuperAdmin)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "superadmins");
        }
        else if (rol == "Profesional" && userId is not null)
        {
            // Recibe solo sus propios turnos
            await Groups.AddToGroupAsync(Context.ConnectionId, $"profesional-{userId}");
        }
        else if (sucursalId is not null)
        {
            // Administrativo, Caja, Owner → reciben todos los turnos de la sucursal
            await Groups.AddToGroupAsync(Context.ConnectionId, $"admins-{sucursalId}");
        }

        await base.OnConnectedAsync();
    }
}
