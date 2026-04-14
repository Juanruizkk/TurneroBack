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

    // Panel SuperAdmin — Suscripciones por sucursal
    Task<IEnumerable<SucursalSuscripcion>> GetTodasConSucursalAsync();
}
