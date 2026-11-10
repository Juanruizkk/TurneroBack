using Microsoft.EntityFrameworkCore;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;

namespace TurneroApp.Infrastructure.Data;

/// <summary>
/// Crea (o resetea) los datos de la sucursal demo "Zen Massage Center".
/// Si la sucursal ya existe, borra y recrea solo los turnos para que el
/// schedule quede limpio sin necesidad de reiniciar la base de datos.
/// </summary>
public static class DemoDataSeeder
{
    private const string DemoSlug = "zen-massage-center";

    // ─── Patrón de turnos por día de la semana ────────────────────────────────
    // Índices de servicio corresponden al orden por DuracionClienteMin:
    //   0 = Masaje Relajante        (45 min)
    //   1 = Masaje Descontracturante (60 min)
    //   2 = Masaje con Piedras       (90 min)
    //
    // Índices de cliente según OrderBy(Nombre):
    //   0=Ana  1=Carlos  2=Elena  3=Juan  4=Luis  5=María
    //
    // Verificación de no solapamiento (duración + 15 min buffer):
    //   Lun: 09:00+60+15=10:15 < 11:00 ✓  11:00+45+15=12:00 < 14:00 ✓  14:00+90=15:30 ✓
    //   Mar: 09:30+45+15=10:30 < 11:00 ✓  11:00+60+15=12:15 < 14:30 ✓  14:30+90=16:00 ✓
    //   Mié: 09:00+90+15=10:45 < 11:30 ✓  11:30+45+15=12:30 < 15:00 ✓  15:00+60=16:00 ✓
    //   Jue: 09:00+60+15=10:15 < 11:00 ✓  11:00+45+15=12:00 < 15:30 ✓  15:30+90=17:00 ✓
    //   Vie: 09:30+45+15=10:30 < 11:00 ✓  11:00+90+15=12:45 < 14:30 ✓  14:30+60=15:30 ✓
    private static readonly Dictionary<DayOfWeek, (int h, int m, int srv, int cli)[]> Patron = new()
    {
        [DayOfWeek.Monday]    = new[] { (9,  0, 1, 0), (11,  0, 0, 3), (14,  0, 2, 1) },
        [DayOfWeek.Tuesday]   = new[] { (9, 30, 0, 4), (11,  0, 1, 2), (14, 30, 2, 5) },
        [DayOfWeek.Wednesday] = new[] { (9,  0, 2, 1), (11, 30, 0, 4), (15,  0, 1, 0) },
        [DayOfWeek.Thursday]  = new[] { (9,  0, 1, 3), (11,  0, 0, 5), (15, 30, 2, 2) },
        [DayOfWeek.Friday]    = new[] { (9, 30, 0, 0), (11,  0, 2, 2), (14, 30, 1, 4) },
    };

    // ─── Entry point ──────────────────────────────────────────────────────────
    public static async Task SeedAsync(AppDbContext db)
    {
        var sucursal = await db.Sucursales.FirstOrDefaultAsync(s => s.Slug == DemoSlug);

        if (sucursal is not null)
        {
            // Sucursal ya existe → resetear turnos y asegurarse de que existan los usuarios demo
            await ResetTurnosAsync(db, sucursal);
            await EnsureUsuariosDemoAsync(db, sucursal);
            return;
        }

        await FullSeedAsync(db);
    }

    // ─── Reset de turnos (sucursal existente) ─────────────────────────────────
    private static async Task ResetTurnosAsync(AppDbContext db, Sucursal sucursal)
    {
        // Buscar los turnos que ya existen
        var turnoIds = await db.Turnos
            .Where(t => t.SucursalId == sucursal.Id)
            .Select(t => t.Id)
            .ToListAsync();

        if (turnoIds.Count > 0)
        {
            // 1. Borrar pagos de turno
            var pagos = await db.PagosTurno
                .Where(p => turnoIds.Contains(p.TurnoId))
                .ToListAsync();
            db.PagosTurno.RemoveRange(pagos);

            // 2. Borrar movimientos de caja vinculados a esos turnos
            var movs = await db.MovimientosCaja
                .Where(m => m.TurnoId.HasValue && turnoIds.Contains(m.TurnoId.Value))
                .ToListAsync();
            db.MovimientosCaja.RemoveRange(movs);

            // 3. Borrar turnos
            var turnos = await db.Turnos
                .Where(t => t.SucursalId == sucursal.Id)
                .ToListAsync();
            db.Turnos.RemoveRange(turnos);

            await db.SaveChangesAsync();
        }

        // Recuperar entidades necesarias para recrear los turnos
        var caja = await db.Cajas
            .FirstOrDefaultAsync(c => c.SucursalId == sucursal.Id);

        var sesion = await db.CajaSesiones
            .Where(s => s.CajaId == caja!.Id)
            .OrderBy(s => s.FechaApertura)
            .FirstOrDefaultAsync();

        // Si no hay sesión de caja, crearla
        if (caja is not null && sesion is null)
        {
            var profId = await db.UsuarioSucursalRoles
                .Where(r => r.SucursalId == sucursal.Id && r.EsOwner)
                .Select(r => r.UsuarioId)
                .FirstOrDefaultAsync();

            sesion = new CajaSesion
            {
                Id                = Guid.NewGuid(),
                CajaId            = caja.Id,
                UsuarioAperturaId = profId,
                FechaApertura     = new DateTime(2026, 5, 1, 9, 0, 0, DateTimeKind.Unspecified),
                MontoInicial      = 0,
                Estado            = EstadoCajaSesion.Abierta
            };
            db.CajaSesiones.Add(sesion);
            await db.SaveChangesAsync();
        }

        var profesionalId = await db.UsuarioSucursalRoles
            .Where(r => r.SucursalId == sucursal.Id && r.EsOwner)
            .Select(r => r.UsuarioId)
            .FirstOrDefaultAsync();

        // Servicios ordenados por duración: [0]=45m [1]=60m [2]=90m
        var servicios = await db.Servicios
            .Where(s => s.SucursalId == sucursal.Id && s.Activo)
            .OrderBy(s => s.DuracionClienteMin)
            .ToListAsync();

        // Clientes ordenados por nombre: [0]=Ana [1]=Carlos [2]=Elena [3]=Juan [4]=Luis [5]=María
        var clientes = await db.Clientes
            .Where(c => c.SucursalId == sucursal.Id)
            .OrderBy(c => c.Nombre)
            .ToListAsync();

        if (caja is null || sesion is null || servicios.Count < 3 || clientes.Count < 3)
            return;

        CrearTurnos(db, sucursal.Id, profesionalId, caja.Id, sesion.Id, servicios, clientes);
        await db.SaveChangesAsync();
    }

    // ─── Seed completo (sucursal nueva) ───────────────────────────────────────
    // Delega en DemoDatasetBuilder (misma lógica reutilizada por DemoTenantService)
    // para no duplicar la definición del dataset. En dev el slug es fijo y los
    // turnos se siembran relativos a la semana actual (no mayo 2026 fijo).
    private static async Task FullSeedAsync(AppDbContext db)
    {
        var lunes = DemoDatasetBuilder.LunesDeLaSemana(DateTime.Now);
        DemoDatasetBuilder.Build(db, new DemoDatasetBuilder.Opciones(
            Slug: DemoSlug,
            EmailSuffix: "",
            SemanaBase: lunes,
            EsDemo: false,
            DemoExpiraEn: null));
        await db.SaveChangesAsync();
    }

    // ─── Usuarios demo extra (idempotente) ───────────────────────────────────
    /// <summary>
    /// Crea los usuarios demo (Administrativo, Profesional, Caja) si no existen aún.
    /// Se llama tanto en FullSeedAsync como en EnsureUsuariosDemoAsync para idempotencia.
    /// </summary>
    private static async Task EnsureUsuariosDemoAsync(AppDbContext db, Sucursal sucursal)
    {
        var servicios = await db.Servicios
            .Where(s => s.SucursalId == sucursal.Id && s.Activo)
            .ToListAsync();

        var demoUsers = new[]
        {
            new { Email = "laura.gomez@zenmassage.com",    Nombre = "Laura",  Apellido = "Gómez",     Rol = RolUsuario.Administrativo, EsOwner = false },
            new { Email = "martina.lopez@zenmassage.com",  Nombre = "Martina",Apellido = "López",     Rol = RolUsuario.Profesional,    EsOwner = false },
            new { Email = "diego.fernandez@zenmassage.com",Nombre = "Diego",  Apellido = "Fernández", Rol = RolUsuario.Caja,           EsOwner = false },
        };

        foreach (var def in demoUsers)
        {
            // Verificar si el usuario ya está en la sucursal
            var yaExiste = await db.Usuarios
                .AnyAsync(u => u.Email == def.Email);

            if (yaExiste) continue;

            var usuario = new Usuario
            {
                Id            = Guid.NewGuid(),
                Nombre        = def.Nombre,
                Apellido      = def.Apellido,
                Email         = def.Email,
                Telefono      = "000000000",
                PasswordHash  = BCrypt.Net.BCrypt.HashPassword("Demo123!"),
                FechaCreacion = DateTime.UtcNow
            };
            db.Usuarios.Add(usuario);

            var rol = new UsuarioSucursalRol
            {
                Id            = Guid.NewGuid(),
                UsuarioId     = usuario.Id,
                SucursalId    = sucursal.Id,
                Rol           = def.Rol,
                EsOwner       = def.EsOwner,
                Estado        = EstadoUsuario.Activo,
                FechaCreacion = DateTime.UtcNow
            };
            db.UsuarioSucursalRoles.Add(rol);

            // Horarios Lun–Vie 09:00–18:00
            for (int i = 1; i <= 5; i++)
            {
                db.HorariosLaborales.Add(new HorarioLaboral
                {
                    Id                   = Guid.NewGuid(),
                    UsuarioSucursalRolId = rol.Id,
                    DiaSemana            = (DayOfWeek)i,
                    HoraDesde            = new TimeSpan(9, 0, 0),
                    HoraHasta            = new TimeSpan(18, 0, 0),
                    Activo               = true
                });
            }

            // Permisos y servicios según rol
            if (def.Rol == RolUsuario.Profesional)
            {
                // Martina: puede agendar y ver teléfonos, pero NO cargar cobranzas
                foreach (var p in new[] { PermisoSistema.AgendarTurnos, PermisoSistema.EditarTurnos, PermisoSistema.VerTelefonoClientes })
                {
                    db.UsuariosPermisos.Add(new UsuarioPermiso
                    {
                        Id                   = Guid.NewGuid(),
                        UsuarioSucursalRolId = rol.Id,
                        Permiso              = p
                    });
                }
                // Vincular todos los servicios
                foreach (var srv in servicios)
                    db.UsuarioServicios.Add(new UsuarioServicio { UsuarioSucursalRolId = rol.Id, ServicioId = srv.Id });
            }
            else if (def.Rol == RolUsuario.Caja)
            {
                // Diego: CargarCobranzas explícito para que la UI lo refleje correctamente
                db.UsuariosPermisos.Add(new UsuarioPermiso
                {
                    Id                   = Guid.NewGuid(),
                    UsuarioSucursalRolId = rol.Id,
                    Permiso              = PermisoSistema.CargarCobranzas
                });
            }
            // Administrativo no necesita permisos explícitos (acceso completo implícito por rol)
        }

        await db.SaveChangesAsync();
    }

    // ─── Creación de turnos (lógica compartida) ───────────────────────────────
    private static void CrearTurnos(
        AppDbContext db,
        Guid sucursalId,
        Guid profesionalId,
        Guid cajaId,
        Guid sesionId,
        List<Servicio> servicios,   // ordenados por DuracionClienteMin ASC
        List<Cliente>  clientes)    // ordenados por Nombre ASC
    {
        for (int day = 1; day <= 31; day++)
        {
            var fecha = new DateTime(2026, 5, day);
            if (fecha.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            if (!Patron.TryGetValue(fecha.DayOfWeek, out var slots)) continue;

            int semana = (day - 1) / 7;   // 0..4  →  rota clientes entre semanas

            foreach (var (h, m, srvIdx, cliIdx) in slots)
            {
                var servicio = servicios[srvIdx % servicios.Count];
                var cliente  = clientes[(cliIdx + semana) % clientes.Count];

                // DateTimeKind.Unspecified → se serializa SIN 'Z' → el browser
                // lo interpreta como hora local → se muestra tal cual en la UI.
                var inicio = new DateTime(2026, 5, day, h, m, 0, DateTimeKind.Unspecified);
                var fin    = inicio.AddMinutes(servicio.DuracionAgendaMin);

                // Días 1-6 de mayo ya pasaron → Completado; resto → Confirmado
                var estado = day < 7 ? EstadoTurno.Completado : EstadoTurno.Confirmado;

                var turno = new Turno
                {
                    Id              = Guid.NewGuid(),
                    SucursalId      = sucursalId,
                    ClienteId       = cliente.Id,
                    ServicioId      = servicio.Id,
                    ProfesionalId   = profesionalId,
                    FechaHoraInicio = inicio,
                    FechaHoraFin    = fin,
                    Estado          = estado,
                    Origen          = OrigenTurno.Administrativo,
                    PrecioServicio  = servicio.Precio,
                    TotalCobrado    = estado == EstadoTurno.Completado ? servicio.Precio : 0,
                    TokenGestion    = Guid.NewGuid().ToString("N"),
                    FechaCreacion   = inicio.AddDays(-2)
                };
                db.Turnos.Add(turno);

                if (estado == EstadoTurno.Completado)
                {
                    var mov = new MovimientoCaja
                    {
                        Id             = Guid.NewGuid(),
                        CajaId         = cajaId,
                        CajaSesionId   = sesionId,
                        UsuarioId      = profesionalId,
                        TipoMovimiento = TipoMovimientoCaja.CobroTurno,
                        Observaciones  = $"Cobro turno {cliente.Nombre} {cliente.Apellido}",
                        Monto          = servicio.Precio,
                        MedioPago      = MedioPago.Efectivo,
                        Fecha          = fin,
                        TurnoId        = turno.Id
                    };
                    db.MovimientosCaja.Add(mov);

                    db.PagosTurno.Add(new PagoTurno
                    {
                        Id               = Guid.NewGuid(),
                        TurnoId          = turno.Id,
                        MovimientoCajaId = mov.Id,
                        Concepto         = ConceptoPagoTurno.PagoTotal,
                        Monto            = servicio.Precio,
                        Fecha            = fin
                    });
                }
            }
        }
    }
}
