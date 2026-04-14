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
    public async Task<Usuario?> GetByEmailAsync(string email)
    {
        return await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant());
    }

    /// <inheritdoc />
    public async Task<Usuario?> GetByEmailGlobalAsync(string email)
    {
        return await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant());
    }

    /// <inheritdoc />
    public async Task<Usuario?> GetByTokenRecuperacionAsync(string token)
    {
        return await _context.Usuarios
            .FirstOrDefaultAsync(u => u.TokenRecuperacion == token);
    }

    /// <inheritdoc />
    public async Task<Usuario?> GetBySeleccionTokenAsync(string token)
    {
        return await _context.Usuarios
            .FirstOrDefaultAsync(u => u.SeleccionToken == token);
    }
}
