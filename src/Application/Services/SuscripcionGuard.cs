using TurneroApp.Application.Exceptions;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.Services;

/// <summary>
/// Centraliza las validaciones de suscripción que se aplican antes de operaciones sensibles.
/// </summary>
public class SuscripcionGuard
{
    private readonly ISuscripcionRepository _repo;

    private static readonly EstadoSuscripcion[] EstadosVencidos =
    [
        EstadoSuscripcion.PruebVencida,
        EstadoSuscripcion.Vencida,
        EstadoSuscripcion.Cancelada,
    ];

    public SuscripcionGuard(ISuscripcionRepository repo)
    {
        _repo = repo;
    }

    /// <summary>
    /// Verifica que la suscripción esté activa (no vencida, no cancelada).
    /// Si no existe registro de suscripción, se trata como activa (permisivo para sucursales legacy).
    /// Lanza <see cref="SuscripcionVencidaException"/> si el acceso debe bloquearse.
    /// </summary>
    public async Task VerificarActivaAsync(Guid sucursalId)
    {
        var suscripcion = await _repo.GetBySucursalAsync(sucursalId);

        // Sin registro → sucursal legacy, acceso libre
        if (suscripcion is null) return;

        if (EstadosVencidos.Contains(suscripcion.Estado))
        {
            var mensaje = suscripcion.Estado switch
            {
                EstadoSuscripcion.PruebVencida =>
                    "Tu período de prueba venció. Activá tu suscripción para continuar creando contenido.",
                EstadoSuscripcion.Cancelada =>
                    "Tu suscripción fue cancelada. Renovála para continuar.",
                _ =>
                    "Tu suscripción venció. Renovála para continuar.",
            };
            throw new SuscripcionVencidaException(mensaje);
        }
    }

    /// <summary>
    /// Verifica que el plan de la sucursal incluya la funcionalidad de Reportes.
    /// Lanza <see cref="FeatureNoPlanException"/> si el plan no la incluye.
    /// </summary>
    public async Task VerificarTieneReportesAsync(Guid sucursalId)
    {
        var suscripcion = await _repo.GetBySucursalAsync(sucursalId);

        // Sin registro → acceso libre (legacy / sin restricciones)
        if (suscripcion is null) return;

        if (!suscripcion.Plan.TieneReportes)
            throw new FeatureNoPlanException(
                $"Los reportes no están incluidos en el plan {suscripcion.Plan.Nombre}. " +
                "Actualizá tu plan para acceder a esta funcionalidad.");
    }
}
