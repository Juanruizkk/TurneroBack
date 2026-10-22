using Microsoft.EntityFrameworkCore;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;
using TurneroApp.Infrastructure.Data;

namespace TurneroApp.Infrastructure.Repositories;

public class UsuarioSucursalRolRepository : IUsuarioSucursalRolRepository
{
    private readonly AppDbContext _context;

    public UsuarioSucursalRolRepository(AppDbContext context) => _context = context;

    public async Task<UsuarioSucursalRol?> GetByIdAsync(Guid id)
    {
        return await _context.UsuarioSucursalRoles
            .Include(u => u.Usuario)
            .Include(u => u.Sucursal)
            .Include(u => u.Permisos)
            .Include(u => u.HorariosLaborales)
            .Include(u => u.Servicios)
            .AsSplitQuery()
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<UsuarioSucursalRol?> GetByInvitacionTokenAsync(string token)
    {
        return await _context.UsuarioSucursalRoles
            .Include(u => u.Usuario)
            .Include(u => u.Sucursal)
            .Include(u => u.Permisos)
            .FirstOrDefaultAsync(u => u.InvitacionToken == token);
    }

    public async Task<IEnumerable<UsuarioSucursalRol>> GetBySucursalAsync(Guid sucursalId)
    {
        return await _context.UsuarioSucursalRoles
            .Include(u => u.Usuario)
            .Include(u => u.Permisos)
            .Include(u => u.HorariosLaborales)
            .Include(u => u.Servicios)
            .AsSplitQuery()
            .Where(u => u.SucursalId == sucursalId)
            .OrderBy(u => u.Usuario.Apellido)
            .ThenBy(u => u.Usuario.Nombre)
            .ToListAsync();
    }

    public async Task<IEnumerable<UsuarioSucursalRol>> GetByUsuarioAsync(Guid usuarioId)
    {
        return await _context.UsuarioSucursalRoles
            .Include(u => u.Sucursal)
            .Include(u => u.Permisos)
            .Where(u => u.UsuarioId == usuarioId)
            .OrderByDescending(u => u.Estado == Domain.Enums.EstadoUsuario.Activo)
            .ThenByDescending(u => u.EsOwner)
            .ToListAsync();
    }

    public async Task<UsuarioSucursalRol?> GetByUsuarioYSucursalAsync(Guid usuarioId, Guid sucursalId)
    {
        return await _context.UsuarioSucursalRoles
            .Include(u => u.Permisos)
            .FirstOrDefaultAsync(u => u.UsuarioId == usuarioId && u.SucursalId == sucursalId);
    }

    public async Task AddAsync(UsuarioSucursalRol rol) =>
        await _context.UsuarioSucursalRoles.AddAsync(rol);

    public void Update(UsuarioSucursalRol rol) =>
        _context.UsuarioSucursalRoles.Update(rol);

    public Task SaveChangesAsync() =>
        _context.SaveChangesAsync();

    public async Task<int> ContarActivosBySucursalAsync(Guid sucursalId)
    {
        return await _context.UsuarioSucursalRoles
            .Where(u => u.SucursalId == sucursalId &&
                        u.Estado != Domain.Enums.EstadoUsuario.Inactivo)
            .CountAsync();
    }

    public async Task DeleteHardAsync(Guid id)
    {
        var usr = await _context.UsuarioSucursalRoles
            .Include(u => u.Permisos)
            .Include(u => u.HorariosLaborales)
            .Include(u => u.Servicios)
            .AsSplitQuery()
            .FirstOrDefaultAsync(u => u.Id == id);
        if (usr is null) return;
        _context.UsuarioSucursalRoles.Remove(usr);
        await _context.SaveChangesAsync();
    }
}
