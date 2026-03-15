using TurneroApp.Application.DTOs.SuperAdmin;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.Services;

public class SuperAdminService
{
    private readonly ISucursalRepository _sucursalRepo;
    private readonly IUsuarioRepository  _usuarioRepo;

    private static readonly PermisoSistema[] TodosLosPermisos =
    [
        PermisoSistema.AgendarTurnos,
        PermisoSistema.EditarTurnos,
        PermisoSistema.CargarCobranzas,
        PermisoSistema.VerTelefonoClientes,
    ];

    public SuperAdminService(ISucursalRepository sucursalRepo, IUsuarioRepository usuarioRepo)
    {
        _sucursalRepo = sucursalRepo;
        _usuarioRepo  = usuarioRepo;
    }

    // ─── Queries ──────────────────────────────────────────────────────────────

    public async Task<IEnumerable<SucursalSuperAdminDto>> ListarAsync()
    {
        var sucursales = await _sucursalRepo.GetAllConOwnerAsync();
        return sucursales.Select(MapToDto);
    }

    public async Task<SucursalSuperAdminDto?> ObtenerAsync(Guid id)
    {
        var s = await _sucursalRepo.GetByIdConOwnerAsync(id);
        return s is null ? null : MapToDto(s);
    }

    // ─── Crear sucursal + owner ───────────────────────────────────────────────

    public async Task<SucursalSuperAdminDto> CrearConOwnerAsync(CrearSucursalConOwnerDto dto)
    {
        // 1. Sucursal
        var sucursal = new Sucursal
        {
            Id           = Guid.NewGuid(),
            Nombre       = dto.Nombre,
            Direccion    = dto.Direccion,
            Telefono     = dto.Telefono,
            Email        = dto.Email,
            Activa       = true,
            FechaCreacion = DateTime.UtcNow,
        };

        await _sucursalRepo.AddAsync(sucursal);

        // 2. Owner — el email puede repetirse en distintas sucursales (regla de negocio)
        var owner = new Usuario
        {
            Id           = Guid.NewGuid(),
            SucursalId   = sucursal.Id,
            Nombre       = dto.OwnerNombre,
            Apellido     = dto.OwnerApellido,
            Email        = dto.OwnerEmail.ToLowerInvariant(),
            Telefono     = dto.OwnerTelefono,
            PasswordHash = dto.OwnerPassword,   // hash real → cuando se implemente auth
            Rol          = RolUsuario.Administrativo,
            Estado       = EstadoUsuario.Activo,
            EsOwner      = true,
            Permisos     = TodosLosPermisos.Select(p => new UsuarioPermiso
            {
                Id        = Guid.NewGuid(),
                UsuarioId = Guid.Empty, // EF lo resuelve por la navigation
                Permiso   = p,
            }).ToList(),
        };

        // Corregir UsuarioId en los permisos antes de persistir
        foreach (var perm in owner.Permisos)
            perm.UsuarioId = owner.Id;

        await _usuarioRepo.AddAsync(owner);

        // 3. Guardar en una sola transacción (mismo DbContext scope)
        await _sucursalRepo.SaveChangesAsync();

        // Cargar el owner en la sucursal para la respuesta
        sucursal.Usuarios = new List<Usuario> { owner };
        return MapToDto(sucursal);
    }

    // ─── Actualizar datos generales ───────────────────────────────────────────

    public async Task<SucursalSuperAdminDto> ActualizarAsync(Guid id, ActualizarSucursalDto dto)
    {
        var sucursal = await _sucursalRepo.GetByIdConOwnerAsync(id)
            ?? throw new InvalidOperationException($"No se encontró la sucursal con Id {id}.");

        sucursal.Nombre    = dto.Nombre;
        sucursal.Direccion = dto.Direccion;
        sucursal.Telefono  = dto.Telefono;
        sucursal.Email     = dto.Email;

        _sucursalRepo.Update(sucursal);
        await _sucursalRepo.SaveChangesAsync();
        return MapToDto(sucursal);
    }

    // ─── Activar / Inactivar ──────────────────────────────────────────────────

    public async Task<SucursalSuperAdminDto> ToggleActivaAsync(Guid id)
    {
        var sucursal = await _sucursalRepo.GetByIdConOwnerAsync(id)
            ?? throw new InvalidOperationException($"No se encontró la sucursal con Id {id}.");

        sucursal.Activa = !sucursal.Activa;

        _sucursalRepo.Update(sucursal);
        await _sucursalRepo.SaveChangesAsync();
        return MapToDto(sucursal);
    }

    // ─── Mapping ──────────────────────────────────────────────────────────────

    private static SucursalSuperAdminDto MapToDto(Sucursal s)
    {
        var owner = s.Usuarios.FirstOrDefault(u => u.EsOwner);
        return new SucursalSuperAdminDto
        {
            Id           = s.Id,
            Nombre       = s.Nombre,
            Direccion    = s.Direccion,
            Telefono     = s.Telefono,
            Email        = s.Email,
            Activa       = s.Activa,
            FechaCreacion = s.FechaCreacion,
            Owner        = owner is null ? null : new OwnerInfoDto
            {
                Id             = owner.Id,
                Nombre         = owner.Nombre,
                Apellido       = owner.Apellido,
                NombreCompleto = $"{owner.Nombre} {owner.Apellido}",
                Email          = owner.Email,
                Telefono       = owner.Telefono,
                Estado         = owner.Estado.ToString(),
            },
        };
    }
}
