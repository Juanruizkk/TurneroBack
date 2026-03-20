using Microsoft.EntityFrameworkCore;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;
using TurneroApp.Infrastructure.Data;

namespace TurneroApp.Infrastructure.Repositories;

public class UsuarioRepository : GenericRepository<Usuario>, IUsuarioRepository
{
    public UsuarioRepository(AppDbContext context) : base(context)
    {
    }

    /// <inheritdoc />
    public async Task<Usuario?> GetByIdConPermisosAsync(Guid id)
    {
        return await _context.Usuarios
            .Include(u => u.Permisos)
            .Include(u => u.HorariosLaborales)
            .Include(u => u.Servicios)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Usuario>> GetBySucursalAsync(Guid sucursalId)
    {
        return await _context.Usuarios
            .Include(u => u.Permisos)
            .Include(u => u.HorariosLaborales)
            .Include(u => u.Servicios)
            .Where(u => u.SucursalId == sucursalId)
            .OrderBy(u => u.Apellido)
            .ThenBy(u => u.Nombre)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<Usuario?> GetByEmailAsync(Guid sucursalId, string email)
    {
        return await _context.Usuarios
            .FirstOrDefaultAsync(u =>
                u.SucursalId == sucursalId &&
                u.Email == email.ToLowerInvariant());
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Usuario>> GetByEmailGlobalAsync(string email)
    {
        return await _context.Usuarios
            .Include(u => u.Permisos)
            .Where(u => u.Email == email.ToLowerInvariant())
            .OrderByDescending(u => u.Estado == Domain.Enums.EstadoUsuario.Activo)
            .ThenByDescending(u => u.EsOwner)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<Usuario?> GetByInvitacionTokenAsync(string token)
    {
        return await _context.Usuarios
            .Include(u => u.Permisos)
            .Include(u => u.Sucursal)
            .FirstOrDefaultAsync(u => u.InvitacionToken == token);
    }

    /// <inheritdoc />
    public async Task<Usuario?> GetByTokenRecuperacionAsync(string token)
    {
        return await _context.Usuarios
            .FirstOrDefaultAsync(u => u.TokenRecuperacion == token);
    }
}
