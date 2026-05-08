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
            // Sucursal ya existe → solo resetear turnos
            await ResetTurnosAsync(db, sucursal);
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
    private static async Task FullSeedAsync(AppDbContext db)
    {
        // 1. Sucursal
        var sucursal = new Sucursal
        {
            Id            = Guid.NewGuid(),
            Nombre        = "Zen Massage Center",
            Slug          = DemoSlug,
            Slogan        = "Tu momento de paz y relajación",
            Direccion     = "Av. Siempre Viva 742, Springfield",
            Telefono      = "123456789",
            Email         = "contacto@zenmassage.com",
            Moneda        = "ARS",
            Rubros        = new List<string> { "Masajes", "Bienestar", "Spa" },
            FechaCreacion = DateTime.UtcNow
        };
        db.Sucursales.Add(sucursal);

        // 2. Configuración
        db.ConfiguracionesSucursal.Add(new ConfiguracionSucursal
        {
            Id                               = Guid.NewGuid(),
            SucursalId                       = sucursal.Id,
            MinutosAnticipacionMinimaReserva = 60,
            DiasMaximosReservaFutura         = 30,
            MinutosBufferEntreTurnos         = 15,
            PermiteCancelacionCliente        = true,
            HorasMinimasCancelacion          = 2,
            ModoConfirmacionTurnos           = ModoConfirmacionTurnos.Automatico,
            HorariosAtencionJson             = "{}"
        });

        // 3. Caja
        var caja = new Caja
        {
            Id         = Guid.NewGuid(),
            SucursalId = sucursal.Id,
            Nombre     = "Caja Principal",
            Activa     = true
        };
        db.Cajas.Add(caja);

        // 4. Profesional / Owner
        var profesional = new Usuario
        {
            Id            = Guid.NewGuid(),
            Nombre        = "Adrián",
            Apellido      = "Sosa",
            Email         = "adrian.sosa@zenmassage.com",
            Telefono      = "987654321",
            PasswordHash  = BCrypt.Net.BCrypt.HashPassword("Demo123!"),
            FechaCreacion = DateTime.UtcNow
        };
        db.Usuarios.Add(profesional);

        // 5. Rol
        var rol = new UsuarioSucursalRol
        {
            Id            = Guid.NewGuid(),
            UsuarioId     = profesional.Id,
            SucursalId    = sucursal.Id,
            Rol           = RolUsuario.Profesional,
            EsOwner       = true,
            Estado        = EstadoUsuario.Activo,
            FechaCreacion = DateTime.UtcNow
        };
        db.UsuarioSucursalRoles.Add(rol);

        // 6. Horarios laborales Lun–Vie 09:00–18:00
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

        // 7. Servicios  (orden por duración: [0]=45m  [1]=60m  [2]=90m)
        var s0 = new Servicio   // Relajante — 45 min
        {
            Id                  = Guid.NewGuid(),
            SucursalId          = sucursal.Id,
            Nombre              = "Masaje Relajante",
            Descripcion         = "Masaje suave para reducir el estrés.",
            Precio              = 4000,
            DuracionClienteMin  = 45,
            DuracionAgendaMin   = 45,
            Activo              = true,
            ModalidadPrestacion = ModalidadPrestacion.Presencial,
            TipoServicio        = TipoServicio.Regular
        };
        var s1 = new Servicio   // Descontracturante — 60 min
        {
            Id                  = Guid.NewGuid(),
            SucursalId          = sucursal.Id,
            Nombre              = "Masaje Descontracturante",
            Descripcion         = "Masaje profundo para liberar tensiones musculares.",
            Precio              = 5000,
            DuracionClienteMin  = 60,
            DuracionAgendaMin   = 60,
            Activo              = true,
            ModalidadPrestacion = ModalidadPrestacion.Presencial,
            TipoServicio        = TipoServicio.Regular
        };
        var s2 = new Servicio   // Piedras Calientes — 90 min
        {
            Id                  = Guid.NewGuid(),
            SucursalId          = sucursal.Id,
            Nombre              = "Masaje con Piedras Calientes",
            Descripcion         = "Terapia geotermal para relajación profunda.",
            Precio              = 7500,
            DuracionClienteMin  = 90,
            DuracionAgendaMin   = 90,
            Activo              = true,
            ModalidadPrestacion = ModalidadPrestacion.Presencial,
            TipoServicio        = TipoServicio.Regular
        };
        db.Servicios.AddRange(s0, s1, s2);

        // 8. Vincular servicios al profesional
        foreach (var srv in new[] { s0, s1, s2 })
            db.UsuarioServicios.Add(new UsuarioServicio { UsuarioSucursalRolId = rol.Id, ServicioId = srv.Id });

        // 9. Clientes  (orden alfabético por nombre: Ana, Carlos, Elena, Juan, Luis, María)
        var clientes = new List<Cliente>
        {
            new() { Id = Guid.NewGuid(), SucursalId = sucursal.Id, Nombre = "Ana",    Apellido = "Martínez",  Telefono = "1166778899", Email = "ana.mtz@example.com"      },
            new() { Id = Guid.NewGuid(), SucursalId = sucursal.Id, Nombre = "Carlos", Apellido = "Rodríguez", Telefono = "1155443322", Email = "carlos.rod@example.com"   },
            new() { Id = Guid.NewGuid(), SucursalId = sucursal.Id, Nombre = "Elena",  Apellido = "Sánchez",   Telefono = "1144556677", Email = "elena.s@example.com"      },
            new() { Id = Guid.NewGuid(), SucursalId = sucursal.Id, Nombre = "Juan",   Apellido = "Pérez",     Telefono = "1122334455", Email = "juan.perez@example.com"   },
            new() { Id = Guid.NewGuid(), SucursalId = sucursal.Id, Nombre = "Luis",   Apellido = "González",  Telefono = "1133221100", Email = "luis.gonz@example.com"    },
            new() { Id = Guid.NewGuid(), SucursalId = sucursal.Id, Nombre = "María",  Apellido = "García",    Telefono = "1199887766", Email = "maria.garcia@example.com" },
        };
        db.Clientes.AddRange(clientes);

        // 10. Sesión de caja
        var sesion = new CajaSesion
        {
            Id                = Guid.NewGuid(),
            CajaId            = caja.Id,
            UsuarioAperturaId = profesional.Id,
            FechaApertura     = new DateTime(2026, 5, 1, 9, 0, 0, DateTimeKind.Unspecified),
            MontoInicial      = 0,
            Estado            = EstadoCajaSesion.Abierta
        };
        db.CajaSesiones.Add(sesion);

        // Guardar todo antes de crear turnos (para que los FK existan)
        await db.SaveChangesAsync();

        // 11. Turnos
        var serviciosList = new List<Servicio> { s0, s1, s2 };
        CrearTurnos(db, sucursal.Id, profesional.Id, caja.Id, sesion.Id, serviciosList, clientes);

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
