using Microsoft.EntityFrameworkCore;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;
using TurneroApp.Infrastructure.Data;

namespace TurneroApp.Infrastructure.Repositories;

public class SuscripcionRepository : GenericRepository<SucursalSuscripcion>, ISuscripcionRepository
{
    public SuscripcionRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<SucursalSuscripcion?> GetBySucursalAsync(Guid sucursalId)
    {
        return await _dbSet
            .Include(s => s.Plan)
            .Where(s => s.SucursalId == sucursalId)
            .OrderByDescending(s => s.FechaInicio)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<Plan>> GetPlanesActivosAsync()
    {
        return await _context.Set<Plan>()
            .Where(p => p.EsActivo)
            .OrderBy(p => p.OrdenDisplay)
            .ToListAsync();
    }

    public async Task<Plan?> GetPlanByIdAsync(Guid planId)
    {
        return await _context.Set<Plan>().FindAsync(planId);
    }

    public async Task<Plan?> GetPlanBasicoAsync()
    {
        return await _context.Set<Plan>()
            .Where(p => p.EsActivo)
            .OrderBy(p => p.OrdenDisplay)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<Plan>> GetTodosLosPlanesAsync()
    {
        return await _context.Set<Plan>()
            .OrderBy(p => p.OrdenDisplay)
            .ToListAsync();
    }

    public async Task AddPlanAsync(Plan plan)
    {
        await _context.Set<Plan>().AddAsync(plan);
    }

    public void UpdatePlan(Plan plan)
    {
        _context.Set<Plan>().Update(plan);
    }

    public async Task<IEnumerable<SucursalSuscripcion>> GetTodasConSucursalAsync()
    {
        return await _dbSet
            .Include(s => s.Plan)
            .Include(s => s.Sucursal)
            .OrderBy(s => s.Sucursal.Nombre)
            .ToListAsync();
    }
}
