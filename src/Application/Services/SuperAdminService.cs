using TurneroApp.Application.DTOs.SuperAdmin;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.Services;

public class SuperAdminService
{
    private readonly ISucursalRepository _sucursalRepo;
    private readonly IUsuarioRepository  _usuarioRepo;
    private readonly IUsuarioSucursalRolRepository _usrRepo;
    private readonly ICajaRepository     _cajaRepo;
    private readonly ISuscripcionRepository _suscripcionRepo;

    private static readonly PermisoSistema[] TodosLosPermisos =
    [
        PermisoSistema.AgendarTurnos,
        PermisoSistema.EditarTurnos,
        PermisoSistema.CargarCobranzas,
        PermisoSistema.VerTelefonoClientes,
    ];

    public SuperAdminService(
        ISucursalRepository sucursalRepo,
        IUsuarioRepository  usuarioRepo,
        IUsuarioSucursalRolRepository usrRepo,
        ICajaRepository     cajaRepo,
        ISuscripcionRepository suscripcionRepo)
    {
        _sucursalRepo    = sucursalRepo;
        _usuarioRepo     = usuarioRepo;
        _usrRepo         = usrRepo;
        _cajaRepo        = cajaRepo;
        _suscripcionRepo = suscripcionRepo;
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
            Id            = Guid.NewGuid(),
            Nombre        = dto.Nombre,
            Direccion     = dto.Direccion,
            Telefono      = dto.Telefono,
            Email         = dto.Email,
            Activa        = true,
            FechaCreacion = DateTime.UtcNow,
        };

        await _sucursalRepo.AddAsync(sucursal);

        // 2. Owner — buscar si ya existe un Usuario global con ese email
        var emailNorm      = dto.OwnerEmail.ToLowerInvariant();
        var usuarioExist   = await _usuarioRepo.GetByEmailGlobalAsync(emailNorm);
        bool esNuevoUsuario = usuarioExist is null;

        Usuario owner;
        if (esNuevoUsuario)
        {
            owner = new Usuario
            {
                Id            = Guid.NewGuid(),
                Nombre        = dto.OwnerNombre,
                Apellido      = dto.OwnerApellido,
                Email         = emailNorm,
                Telefono      = dto.OwnerTelefono,
                PasswordHash  = dto.OwnerPassword,
                FechaCreacion = DateTime.UtcNow,
            };
            await _usuarioRepo.AddAsync(owner);
        }
        else
        {
            owner = usuarioExist!;
        }

        // 3. Crear membresía owner
        var ownerUsr = new UsuarioSucursalRol
        {
            Id            = Guid.NewGuid(),
            UsuarioId     = owner.Id,
            SucursalId    = sucursal.Id,
            Rol           = RolUsuario.Administrativo,
            EsOwner       = true,
            Estado        = EstadoUsuario.Activo,
            FechaCreacion = DateTime.UtcNow,
            Permisos      = TodosLosPermisos.Select(p => new UsuarioPermiso
            {
                Id     = Guid.NewGuid(),
                Permiso = p,
            }).ToList(),
        };

        // Asignar el UsuarioSucursalRolId en los permisos
        foreach (var perm in ownerUsr.Permisos)
            perm.UsuarioSucursalRolId = ownerUsr.Id;

        await _usrRepo.AddAsync(ownerUsr);

        // 4. Caja de la sucursal
        await _cajaRepo.AddAsync(new Caja
        {
            Id         = Guid.NewGuid(),
            SucursalId = sucursal.Id,
            Nombre     = $"Caja — {dto.Nombre}",
            Activa     = true,
        });

        // 5. Guardar todo en una sola transacción
        await _sucursalRepo.SaveChangesAsync();

        // Cargar el ownerUsr en la sucursal para la respuesta
        ownerUsr.Usuario   = owner;
        sucursal.UsuarioSucursalRoles = new List<UsuarioSucursalRol> { ownerUsr };
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

    // ─── Sincronizar password ─────────────────────────────────────────────────

    /// <summary>
    /// Actualiza la contraseña del usuario global con ese email.
    /// </summary>
    public async Task<int> SincronizarPasswordAsync(string email, string nuevoPassword)
    {
        var emailNorm = email.ToLowerInvariant().Trim();
        var usuario   = await _usuarioRepo.GetByEmailGlobalAsync(emailNorm);

        if (usuario is null)
            throw new InvalidOperationException($"No se encontró ningún usuario activo con el email '{emailNorm}'.");

        usuario.PasswordHash = nuevoPassword;
        _usuarioRepo.Update(usuario);
        await _usuarioRepo.SaveChangesAsync();

        return 1;
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

    // ─── Planes ───────────────────────────────────────────────────────────────

    public async Task<IEnumerable<PlanDto>> ListarPlanesAsync()
    {
        var planes = await _suscripcionRepo.GetTodosLosPlanesAsync();
        return planes.Select(MapPlanToDto);
    }

    public async Task<PlanDto?> ObtenerPlanAsync(Guid id)
    {
        var plan = await _suscripcionRepo.GetPlanByIdAsync(id);
        return plan is null ? null : MapPlanToDto(plan);
    }

    public async Task<PlanDto> CrearPlanAsync(CrearPlanDto dto)
    {
        var plan = new Plan
        {
            Id                = Guid.NewGuid(),
            Nombre            = dto.Nombre,
            Descripcion       = dto.Descripcion,
            MaxUsuarios       = dto.MaxUsuarios,
            MaxTurnosMes      = dto.MaxTurnosMes,
            PrecioMensual     = dto.PrecioMensual,
            EsActivo          = dto.EsActivo,
            OrdenDisplay      = dto.OrdenDisplay,
            DuracionTrialDias = dto.DuracionTrialDias,
            TieneReportes     = dto.TieneReportes,
        };

        await _suscripcionRepo.AddPlanAsync(plan);
        await _suscripcionRepo.SaveChangesAsync();
        return MapPlanToDto(plan);
    }

    public async Task<PlanDto> ActualizarPlanAsync(Guid id, ActualizarPlanDto dto)
    {
        var plan = await _suscripcionRepo.GetPlanByIdAsync(id)
            ?? throw new InvalidOperationException("Plan no encontrado.");

        plan.Nombre            = dto.Nombre;
        plan.Descripcion       = dto.Descripcion;
        plan.MaxUsuarios       = dto.MaxUsuarios;
        plan.MaxTurnosMes      = dto.MaxTurnosMes;
        plan.EsActivo          = dto.EsActivo;
        plan.OrdenDisplay      = dto.OrdenDisplay;
        plan.DuracionTrialDias = dto.DuracionTrialDias;
        plan.TieneReportes     = dto.TieneReportes;

        _suscripcionRepo.UpdatePlan(plan);
        await _suscripcionRepo.SaveChangesAsync();
        return MapPlanToDto(plan);
    }

    public async Task<PlanDto> CambiarPrecioPlanAsync(Guid id, CambiarPrecioPlanDto dto)
    {
        var plan = await _suscripcionRepo.GetPlanByIdAsync(id)
            ?? throw new InvalidOperationException("Plan no encontrado.");

        plan.PrecioMensual = dto.PrecioMensual;

        _suscripcionRepo.UpdatePlan(plan);
        await _suscripcionRepo.SaveChangesAsync();
        return MapPlanToDto(plan);
    }

    public async Task<PlanDto> ToggleActivoPlanAsync(Guid id)
    {
        var plan = await _suscripcionRepo.GetPlanByIdAsync(id)
            ?? throw new InvalidOperationException("Plan no encontrado.");

        plan.EsActivo = !plan.EsActivo;

        _suscripcionRepo.UpdatePlan(plan);
        await _suscripcionRepo.SaveChangesAsync();
        return MapPlanToDto(plan);
    }

    // ─── Suscripciones ────────────────────────────────────────────────────────

    public async Task<IEnumerable<SuscripcionSuperAdminDto>> ListarSuscripcionesAsync()
    {
        var suscripciones = await _suscripcionRepo.GetTodasConSucursalAsync();
        var now = DateTime.UtcNow;
        return suscripciones.Select(s => MapSuscripcionToDto(s, now));
    }

    public async Task<SuscripcionSuperAdminDto> CambiarPlanSuscripcionAsync(Guid sucursalId, CambiarPlanSuperAdminDto dto)
    {
        var suscripcion = await _suscripcionRepo.GetBySucursalAsync(sucursalId)
            ?? throw new InvalidOperationException("No se encontró suscripción para la sucursal indicada.");

        var plan = await _suscripcionRepo.GetPlanByIdAsync(dto.PlanId)
            ?? throw new InvalidOperationException("Plan no encontrado.");

        suscripcion.PlanId = dto.PlanId;
        suscripcion.Plan   = plan;

        _suscripcionRepo.Update(suscripcion);
        await _suscripcionRepo.SaveChangesAsync();

        var sucursal = await _sucursalRepo.GetByIdConOwnerAsync(sucursalId)
            ?? throw new InvalidOperationException("Sucursal no encontrada.");

        var now = DateTime.UtcNow;
        return new SuscripcionSuperAdminDto
        {
            SucursalId       = suscripcion.SucursalId,
            SucursalNombre   = sucursal.Nombre,
            SucursalSlug     = sucursal.Slug,
            PlanId           = suscripcion.PlanId,
            PlanNombre       = plan.Nombre,
            Estado           = suscripcion.Estado.ToString(),
            EnPeriodoPrueba  = suscripcion.EnPeriodoPrueba,
            FechaVencimiento = suscripcion.FechaVencimiento,
            DiasRestantes    = (int)Math.Ceiling(Math.Max(0, (suscripcion.FechaVencimiento - now).TotalDays)),
        };
    }

    private static SuscripcionSuperAdminDto MapSuscripcionToDto(SucursalSuscripcion s, DateTime now) => new()
    {
        SucursalId       = s.SucursalId,
        SucursalNombre   = s.Sucursal.Nombre,
        SucursalSlug     = s.Sucursal.Slug,
        PlanId           = s.PlanId,
        PlanNombre       = s.Plan.Nombre,
        Estado           = s.Estado.ToString(),
        EnPeriodoPrueba  = s.EnPeriodoPrueba,
        FechaVencimiento = s.FechaVencimiento,
        DiasRestantes    = (int)Math.Ceiling(Math.Max(0, (s.FechaVencimiento - now).TotalDays)),
    };

    private static PlanDto MapPlanToDto(Plan p) => new()
    {
        Id                = p.Id,
        Nombre            = p.Nombre,
        Descripcion       = p.Descripcion,
        MaxUsuarios       = p.MaxUsuarios,
        MaxTurnosMes      = p.MaxTurnosMes,
        PrecioMensual     = p.PrecioMensual,
        EsActivo          = p.EsActivo,
        OrdenDisplay      = p.OrdenDisplay,
        DuracionTrialDias = p.DuracionTrialDias,
        TieneReportes     = p.TieneReportes,
    };

    // ─── Mapping ──────────────────────────────────────────────────────────────

    private static SucursalSuperAdminDto MapToDto(Sucursal s)
    {
        var ownerUsr = s.UsuarioSucursalRoles.FirstOrDefault(u => u.EsOwner);
        var owner    = ownerUsr?.Usuario;

        return new SucursalSuperAdminDto
        {
            Id            = s.Id,
            Nombre        = s.Nombre,
            Slug          = s.Slug,
            Direccion     = s.Direccion,
            Telefono      = s.Telefono,
            Email         = s.Email,
            Activa        = s.Activa,
            FechaCreacion = s.FechaCreacion,
            Owner         = owner is null ? null : new OwnerInfoDto
            {
                Id             = owner.Id,
                Nombre         = owner.Nombre,
                Apellido       = owner.Apellido,
                NombreCompleto = $"{owner.Nombre} {owner.Apellido}",
                Email          = owner.Email,
                Telefono       = owner.Telefono,
                Estado         = ownerUsr!.Estado.ToString(),
            },
        };
    }
}
