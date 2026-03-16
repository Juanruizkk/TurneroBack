using Microsoft.EntityFrameworkCore;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;
using TurneroApp.Infrastructure.Data;

namespace TurneroApp.Infrastructure.Repositories;

public class ClienteRepository : GenericRepository<Cliente>, IClienteRepository
{
    public ClienteRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Cliente>> GetBySucursalAsync(Guid sucursalId)
    {
        return await _dbSet
            .Where(c => c.SucursalId == sucursalId)
            .OrderBy(c => c.Apellido)
            .ThenBy(c => c.Nombre)
            .ToListAsync();
    }

    public async Task<Cliente?> BuscarPorTelefonoAsync(Guid sucursalId, string telefono)
    {
        return await _dbSet
            .FirstOrDefaultAsync(c => c.SucursalId == sucursalId && c.Telefono == telefono);
    }

    public async Task<Cliente?> BuscarPorEmailAsync(Guid sucursalId, string email)
    {
        return await _dbSet
            .FirstOrDefaultAsync(c => c.SucursalId == sucursalId
                                   && c.Email != null
                                   && c.Email.ToLower() == email.ToLower());
    }

    public async Task<IEnumerable<Cliente>> BuscarAsync(Guid sucursalId, string q, int max = 10)
    {
        var ql = q.ToLower();
        return await _dbSet
            .Where(c => c.SucursalId == sucursalId && (
                c.Nombre.ToLower().Contains(ql) ||
                c.Apellido.ToLower().Contains(ql) ||
                c.Telefono.Contains(q) ||
                (c.Email != null && c.Email.ToLower().Contains(ql))
            ))
            .OrderBy(c => c.Apellido)
            .ThenBy(c => c.Nombre)
            .Take(max)
            .ToListAsync();
    }
}
