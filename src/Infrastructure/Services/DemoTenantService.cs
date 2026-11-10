using Microsoft.EntityFrameworkCore;
using TurneroApp.Application.DTOs.Demo;
using TurneroApp.Application.Exceptions;
using TurneroApp.Application.Interfaces;
using TurneroApp.Infrastructure.Data;

namespace TurneroApp.Infrastructure.Services;

/// <summary>
/// Crea sucursales demo efímeras y hace el teardown ordenado por SucursalId.
/// Todas las queries usan IgnoreQueryFilters porque el servicio corre sin tenant
/// (background service / request anónimo) o con uno distinto al de la demo.
/// </summary>
public class DemoTenantService : IDemoTenantService
{
    private readonly AppDbContext _db;
    private readonly int _ttlMinutos;
    private readonly int _maxPorIp;
    private readonly int _maxGlobal;

    public DemoTenantService(AppDbContext db, int ttlMinutos, int maxPorIp, int maxGlobal)
    {
        _db         = db;
        _ttlMinutos = ttlMinutos;
        _maxPorIp   = maxPorIp;
        _maxGlobal  = maxGlobal;
    }

    public async Task<DemoCreacionResultado> CrearAsync(string ip, CancellationToken ct = default)
    {
        var ahora = DateTime.UtcNow;

        var activasGlobal = await _db.Sucursales.IgnoreQueryFilters()
            .CountAsync(s => s.EsDemo && s.DemoExpiraEn > ahora, ct);
        if (activasGlobal >= _maxGlobal)
            throw new DemoLimiteException("La demo está a plena capacidad en este momento. Probá de nuevo en unos minutos.");

        var activasIp = await _db.Sucursales.IgnoreQueryFilters()
            .CountAsync(s => s.EsDemo && s.DemoExpiraEn > ahora && s.DemoIp == ip, ct);
        if (activasIp >= _maxPorIp)
            throw new DemoLimiteException("Ya tenés varias demos activas. Esperá a que expiren o seguí usando una de las que creaste.");

        var sucursalId = Guid.NewGuid();   // se usa para derivar el sufijo único de emails
        var suffix     = "+" + sucursalId.ToString("N")[..6];
        var expira     = ahora.AddMinutes(_ttlMinutos);
        var lunes      = DemoDatasetBuilder.LunesDeLaSemana(DateTime.Now);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var sucursal = DemoDatasetBuilder.Build(_db, new DemoDatasetBuilder.Opciones(
            Slug: "demo-" + sucursalId.ToString("N")[..8],
            EmailSuffix: suffix,
            SemanaBase: lunes,
            EsDemo: true,
            DemoExpiraEn: expira));
        sucursal.DemoIp = ip;

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        // Recolectar los 4 usuarios recién creados con su rol/permisos para mintear los JWT.
        var roles = await _db.UsuarioSucursalRoles.IgnoreQueryFilters()
            .Include(r => r.Usuario)
            .Include(r => r.Permisos)
            .Where(r => r.SucursalId == sucursal.Id)
            .ToListAsync(ct);

        return new DemoCreacionResultado
        {
            SucursalId = sucursal.Id,
            ExpiraEn   = expira,
            Usuarios   = roles.Select(r => new DemoUsuarioCreado
            {
                UsuarioId  = r.Usuario.Id,
                SucursalId = sucursal.Id,
                Rol        = r.Rol.ToString(),
                Nombre     = r.Usuario.Nombre,
                Apellido   = r.Usuario.Apellido,
                Email      = r.Usuario.Email,
                EsOwner    = r.EsOwner,
                Permisos   = r.Permisos.Select(p => p.Permiso.ToString()).ToList(),
            }).ToList()
        };
    }

    public async Task TeardownAsync(Guid sucursalId, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var turnoIds = await _db.Turnos.IgnoreQueryFilters()
            .Where(t => t.SucursalId == sucursalId).Select(t => t.Id).ToListAsync(ct);

        var cajaIds = await _db.Cajas.IgnoreQueryFilters()
            .Where(c => c.SucursalId == sucursalId).Select(c => c.Id).ToListAsync(ct);

        var rolIds = await _db.UsuarioSucursalRoles.IgnoreQueryFilters()
            .Where(r => r.SucursalId == sucursalId).Select(r => r.Id).ToListAsync(ct);

        var usuarioIds = await _db.UsuarioSucursalRoles.IgnoreQueryFilters()
            .Where(r => r.SucursalId == sucursalId).Select(r => r.UsuarioId).Distinct().ToListAsync(ct);

        var servicioIds = await _db.Servicios.IgnoreQueryFilters()
            .Where(s => s.SucursalId == sucursalId).Select(s => s.Id).ToListAsync(ct);

        // ── 1. Hijos de Turno ──────────────────────────────────────────────────
        await _db.PagosTurno.IgnoreQueryFilters()
            .Where(p => turnoIds.Contains(p.TurnoId)).ExecuteDeleteAsync(ct);
        await _db.MovimientosCaja.IgnoreQueryFilters()
            .Where(m => cajaIds.Contains(m.CajaId)).ExecuteDeleteAsync(ct);
        await _db.AdjuntosTurno.IgnoreQueryFilters()
            .Where(a => turnoIds.Contains(a.TurnoId)).ExecuteDeleteAsync(ct);
        await _db.ComentariosTurno.IgnoreQueryFilters()
            .Where(c => turnoIds.Contains(c.TurnoId)).ExecuteDeleteAsync(ct);
        await _db.Notificaciones.IgnoreQueryFilters()
            .Where(n => n.SucursalId == sucursalId).ExecuteDeleteAsync(ct);

        // ── 2. Turnos ───────────────────────────────────────────────────────────
        await _db.Turnos.IgnoreQueryFilters()
            .Where(t => t.SucursalId == sucursalId).ExecuteDeleteAsync(ct);

        // ── 3. Hijos de UsuarioSucursalRol ──────────────────────────────────────
        await _db.UsuarioServicios.IgnoreQueryFilters()
            .Where(us => rolIds.Contains(us.UsuarioSucursalRolId)).ExecuteDeleteAsync(ct);
        await _db.UsuariosPermisos.IgnoreQueryFilters()
            .Where(up => rolIds.Contains(up.UsuarioSucursalRolId)).ExecuteDeleteAsync(ct);
        await _db.HorariosLaborales.IgnoreQueryFilters()
            .Where(h => rolIds.Contains(h.UsuarioSucursalRolId)).ExecuteDeleteAsync(ct);

        // ── 4. Membresías ───────────────────────────────────────────────────────
        await _db.UsuarioSucursalRoles.IgnoreQueryFilters()
            .Where(r => r.SucursalId == sucursalId).ExecuteDeleteAsync(ct);

        // ── 5. Catálogo y agenda ────────────────────────────────────────────────
        await _db.ServicioDiasDisponibles.IgnoreQueryFilters()
            .Where(d => servicioIds.Contains(d.ServicioId)).ExecuteDeleteAsync(ct);
        await _db.BloqueosAgenda.IgnoreQueryFilters()
            .Where(b => b.SucursalId == sucursalId).ExecuteDeleteAsync(ct);
        await _db.Clientes.IgnoreQueryFilters()
            .Where(c => c.SucursalId == sucursalId).ExecuteDeleteAsync(ct);
        await _db.Servicios.IgnoreQueryFilters()
            .Where(s => s.SucursalId == sucursalId).ExecuteDeleteAsync(ct);

        // ── 6. Caja ─────────────────────────────────────────────────────────────
        await _db.CajaSesiones.IgnoreQueryFilters()
            .Where(cs => cajaIds.Contains(cs.CajaId)).ExecuteDeleteAsync(ct);
        await _db.Cajas.IgnoreQueryFilters()
            .Where(c => c.SucursalId == sucursalId).ExecuteDeleteAsync(ct);

        // ── 7. Configuración ────────────────────────────────────────────────────
        await _db.ConfiguracionesSucursal.IgnoreQueryFilters()
            .Where(cfg => cfg.SucursalId == sucursalId).ExecuteDeleteAsync(ct);

        // ── 8. Usuarios (solo los que quedaron sin ninguna otra membresía) ──────
        var usuariosHuerfanos = new List<Guid>();
        foreach (var uid in usuarioIds)
        {
            var tieneOtras = await _db.UsuarioSucursalRoles.IgnoreQueryFilters()
                .AnyAsync(r => r.UsuarioId == uid, ct);
            if (!tieneOtras) usuariosHuerfanos.Add(uid);
        }
        await _db.Usuarios.IgnoreQueryFilters()
            .Where(u => usuariosHuerfanos.Contains(u.Id)).ExecuteDeleteAsync(ct);

        // ── 8.5. Suscripción y sus pagos ────────────────────────────────────────
        // Normalmente una demo no tiene suscripción, pero si la tuviera la FK
        // bloquearía el borrado de la sucursal y la demo quedaría colgada.
        await _db.PagosSuscripcion.IgnoreQueryFilters()
            .Where(p => p.SucursalId == sucursalId).ExecuteDeleteAsync(ct);
        await _db.SucursalSuscripciones.IgnoreQueryFilters()
            .Where(s => s.SucursalId == sucursalId).ExecuteDeleteAsync(ct);

        // ── 9. Sucursal ─────────────────────────────────────────────────────────
        await _db.Sucursales.IgnoreQueryFilters()
            .Where(s => s.Id == sucursalId).ExecuteDeleteAsync(ct);

        await tx.CommitAsync(ct);
    }

    public async Task<int> LimpiarVencidasAsync(CancellationToken ct = default)
    {
        var ahora = DateTime.UtcNow;
        var vencidas = await _db.Sucursales.IgnoreQueryFilters()
            .Where(s => s.EsDemo && s.DemoExpiraEn != null && s.DemoExpiraEn < ahora)
            .Select(s => s.Id).ToListAsync(ct);

        foreach (var id in vencidas)
            await TeardownAsync(id, ct);

        return vencidas.Count;
    }
}
