using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;

namespace TurneroApp.Tests.Helpers;

internal static class SuscripcionTestFactory
{
    public static Plan CrearPlan(
        int maxUsuarios = 2,
        bool tieneReportes = false,
        int? maxTurnosMes = 200,
        string nombre = "Básico")
    {
        return new Plan
        {
            Id                = Guid.NewGuid(),
            Nombre            = nombre,
            MaxUsuarios       = maxUsuarios,
            MaxTurnosMes      = maxTurnosMes,
            PrecioMensual     = 0,
            EsActivo          = true,
            OrdenDisplay      = 1,
            DuracionTrialDias = 14,
            TieneReportes     = tieneReportes,
        };
    }

    public static SucursalSuscripcion CrearSuscripcion(
        Plan? plan = null,
        EstadoSuscripcion estado = EstadoSuscripcion.PruebActiva,
        DateTime? fechaVencimiento = null,
        bool enPeriodoPrueba = true)
    {
        var p = plan ?? CrearPlan();
        return new SucursalSuscripcion
        {
            Id               = Guid.NewGuid(),
            SucursalId       = Guid.NewGuid(),
            PlanId           = p.Id,
            Plan             = p,
            Estado           = estado,
            FechaInicio      = DateTime.UtcNow.AddDays(-14),
            FechaVencimiento = fechaVencimiento ?? DateTime.UtcNow.AddDays(14),
            EnPeriodoPrueba  = enPeriodoPrueba,
        };
    }
}
