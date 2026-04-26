using TurneroApp.Domain.Entities;

namespace TurneroApp.Application.Interfaces;

public interface ISuscripcionRepository : IGenericRepository<SucursalSuscripcion>
{
    Task<SucursalSuscripcion?> GetBySucursalAsync(Guid sucursalId);
    Task<IEnumerable<Plan>> GetPlanesActivosAsync();
    Task<Plan?> GetPlanByIdAsync(Guid planId);
    Task<Plan?> GetPlanBasicoAsync();

    // Panel SuperAdmin
    Task<IEnumerable<Plan>> GetTodosLosPlanesAsync();
    Task AddPlanAsync(Plan plan);
    void UpdatePlan(Plan plan);

    /// <summary>Actualiza solo el Estado de la suscripción sin afectar entidades relacionadas (Plan, Sucursal).</summary>
    void UpdateEstado(SucursalSuscripcion suscripcion);

    // Panel SuperAdmin — Suscripciones por sucursal
    Task<IEnumerable<SucursalSuscripcion>> GetTodasConSucursalAsync();

    // Historial de pagos de suscripción
    Task AddPagoAsync(PagoSuscripcion pago);
    Task<bool> ExistePagoAsync(long mpPaymentId);
    Task<IEnumerable<PagoSuscripcion>> GetHistorialBySucursalAsync(Guid sucursalId, int limit = 20);
}
