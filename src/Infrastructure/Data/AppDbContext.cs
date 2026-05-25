using Microsoft.EntityFrameworkCore;
using TurneroApp.Domain.Entities;
using TurneroApp.Infrastructure.MultiTenant;

namespace TurneroApp.Infrastructure.Data;

public class AppDbContext : DbContext
{
    private readonly ITenantContext? _tenantContext;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext? tenantContext = null)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Sucursal> Sucursales => Set<Sucursal>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<UsuarioSucursalRol> UsuarioSucursalRoles => Set<UsuarioSucursalRol>();
    public DbSet<UsuarioPermiso> UsuariosPermisos => Set<UsuarioPermiso>();
    public DbSet<HorarioLaboral> HorariosLaborales => Set<HorarioLaboral>();
    public DbSet<UsuarioServicio> UsuarioServicios => Set<UsuarioServicio>();
    public DbSet<Servicio> Servicios => Set<Servicio>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Turno> Turnos => Set<Turno>();
    public DbSet<ComentarioTurno> ComentariosTurno => Set<ComentarioTurno>();
    public DbSet<AdjuntoTurno> AdjuntosTurno => Set<AdjuntoTurno>();
    public DbSet<Caja> Cajas => Set<Caja>();
    public DbSet<MovimientoCaja> MovimientosCaja => Set<MovimientoCaja>();
    public DbSet<PagoTurno> PagosTurno => Set<PagoTurno>();
    public DbSet<BloqueoAgenda> BloqueosAgenda => Set<BloqueoAgenda>();
    public DbSet<ConfiguracionSucursal> ConfiguracionesSucursal => Set<ConfiguracionSucursal>();
    public DbSet<CajaSesion> CajaSesiones => Set<CajaSesion>();

    // ─── Suscripciones ───────────────────────────────────────────────────────
    public DbSet<Plan> Planes => Set<Plan>();
    public DbSet<SucursalSuscripcion> SucursalSuscripciones => Set<SucursalSuscripcion>();
    public DbSet<PagoSuscripcion> PagosSuscripcion => Set<PagoSuscripcion>();

    // ─── Mercado Pago OAuth ───────────────────────────────────────────────────
    public DbSet<SucursalMercadoPago> SucursalMercadoPagos => Set<SucursalMercadoPago>();

    // ─── Notificaciones ──────────────────────────────────────────────────────
    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();

    // ─── Días disponibles por servicio ───────────────────────────────────────
    public DbSet<ServicioDiaDisponible> ServicioDiasDisponibles => Set<ServicioDiaDisponible>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // ─── Global Query Filters (multi-tenant) ─────────────────────────────
        // El filtro solo se activa cuando hay un SucursalId en el contexto JWT.
        // SuperAdmin y endpoints anónimos (register, booking público) no tienen
        // SucursalId en el token, por lo que no se filtran.
        modelBuilder.Entity<Servicio>()
            .HasQueryFilter(e =>
                e.Activo &&
                (_tenantContext == null
                 || !_tenantContext.SucursalId.HasValue
                 || e.SucursalId == _tenantContext.SucursalId.GetValueOrDefault()));

        modelBuilder.Entity<Cliente>()
            .HasQueryFilter(e =>
                e.Activo &&
                (_tenantContext == null
                 || !_tenantContext.SucursalId.HasValue
                 || e.SucursalId == _tenantContext.SucursalId.GetValueOrDefault()));

        modelBuilder.Entity<Turno>()
            .HasQueryFilter(e => _tenantContext == null
                || !_tenantContext.SucursalId.HasValue
                || e.SucursalId == _tenantContext.SucursalId.GetValueOrDefault());

        modelBuilder.Entity<Caja>()
            .HasQueryFilter(e =>
                e.Activa &&
                (_tenantContext == null
                 || !_tenantContext.SucursalId.HasValue
                 || e.SucursalId == _tenantContext.SucursalId.GetValueOrDefault()));

        modelBuilder.Entity<BloqueoAgenda>()
            .HasQueryFilter(e => _tenantContext == null
                || !_tenantContext.SucursalId.HasValue
                || e.SucursalId == _tenantContext.SucursalId.GetValueOrDefault());

        modelBuilder.Entity<UsuarioSucursalRol>()
            .HasQueryFilter(e => _tenantContext == null
                || !_tenantContext.SucursalId.HasValue
                || e.SucursalId == _tenantContext.SucursalId.GetValueOrDefault());

        modelBuilder.Entity<SucursalSuscripcion>()
            .HasQueryFilter(e => _tenantContext == null
                || !_tenantContext.SucursalId.HasValue
                || e.SucursalId == _tenantContext.SucursalId.GetValueOrDefault());
    }
}
