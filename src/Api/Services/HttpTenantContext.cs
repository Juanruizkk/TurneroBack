using TurneroApp.Infrastructure.MultiTenant;

namespace TurneroApp.Api.Services;

/// <summary>
/// Lee el claim "sucursalId" del JWT del request HTTP actual.
/// </summary>
public class HttpTenantContext : ITenantContext
{
    public Guid? SucursalId { get; }

    public HttpTenantContext(IHttpContextAccessor httpContextAccessor)
    {
        var claim = httpContextAccessor.HttpContext?.User?.FindFirst("sucursalId");
        if (claim is not null && Guid.TryParse(claim.Value, out var id))
            SucursalId = id;
    }
}
