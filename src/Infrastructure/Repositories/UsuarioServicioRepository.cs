using Microsoft.EntityFrameworkCore;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;
using TurneroApp.Infrastructure.Data;

namespace TurneroApp.Infrastructure.Repositories;

public class UsuarioServicioRepository : IUsuarioServicioRepository
{
    private readonly AppDbContext _context;

    public UsuarioServicioRepository(AppDbContext context) => _context = context;

    /// <inheritdoc />
    public Task<List<UsuarioServicio>> GetByUsuarioIdAsync(Guid usuarioId) =>
        _context.UsuarioServicios
            .Where(us => us.UsuarioSucursalRol.UsuarioId == usuarioId)
            .ToListAsync();

    /// <inheritdoc />
    public async Task<Dictionary<Guid, List<UsuarioServicio>>> GetByUsuariosAsync(IEnumerable<Guid> usuarioIds)
    {
        var ids = usuarioIds.ToList();
        var lista = await _context.UsuarioServicios
            .Include(us => us.UsuarioSucursalRol)
            .Where(us => ids.Contains(us.UsuarioSucursalRol.UsuarioId))
            .ToListAsync();
        return lista
            .GroupBy(us => us.UsuarioSucursalRol.UsuarioId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <inheritdoc />
    public async Task ReplaceAsync(Guid usuarioSucursalRolId, IEnumerable<Guid> servicioIds)
    {
        var existentes = await _context.UsuarioServicios
            .Where(us => us.UsuarioSucursalRolId == usuarioSucursalRolId)
            .ToListAsync();

        _context.UsuarioServicios.RemoveRange(existentes);

        var nuevos = servicioIds.Select(sId => new UsuarioServicio
        {
            UsuarioSucursalRolId = usuarioSucursalRolId,
            ServicioId           = sId,
        });
        await _context.UsuarioServicios.AddRangeAsync(nuevos);
    }

    /// <inheritdoc />
    public async Task AsignarServicioASucursalAsync(Guid sucursalId, Guid servicioId)
    {
        // IDs de membresías activas en la sucursal
        var usrIds = await _context.UsuarioSucursalRoles
            .Where(usr => usr.SucursalId == sucursalId && usr.Estado == EstadoUsuario.Activo)
            .Select(usr => usr.Id)
            .ToListAsync();

        if (usrIds.Count == 0) return;

        // Solo los que aún no tienen el servicio
        var yaAsignados = await _context.UsuarioServicios
            .Where(us => usrIds.Contains(us.UsuarioSucursalRolId) && us.ServicioId == servicioId)
            .Select(us => us.UsuarioSucursalRolId)
            .ToListAsync();

        var nuevos = usrIds
            .Except(yaAsignados)
            .Select(rolId => new UsuarioServicio { UsuarioSucursalRolId = rolId, ServicioId = servicioId });

        await _context.UsuarioServicios.AddRangeAsync(nuevos);
    }

    public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();
}
