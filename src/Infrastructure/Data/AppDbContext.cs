using Microsoft.EntityFrameworkCore;
using TurneroApp.Domain.Entities;

namespace TurneroApp.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Sucursal> Sucursales => Set<Sucursal>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<UsuarioPermiso> UsuariosPermisos => Set<UsuarioPermiso>();
    public DbSet<HorarioLaboral> HorariosLaborales => Set<HorarioLaboral>();
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
