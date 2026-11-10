using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;

namespace TurneroApp.Infrastructure.Data;

/// <summary>
/// Construye el dataset "Zen Massage Center" dentro de un AppDbContext,
/// parametrizado para reuso en seeding de desarrollo y en demos efímeras por visitante.
/// No llama a SaveChanges: es responsabilidad del caller.
/// </summary>
public static class DemoDatasetBuilder
{
    public record Opciones(
        string Slug,
        string EmailSuffix,   // "" en dev; "+a1b2c3" en demo
        DateTime SemanaBase,  // lunes de la semana a poblar (Date, Kind=Unspecified)
        bool EsDemo,
        DateTime? DemoExpiraEn);

    // Patrón Lun–Vie (igual al de DemoDataSeeder): (hora, min, idxServicio, idxCliente).
    // Servicios ordenados por duración: 0=45m 1=60m 2=90m.
    // Clientes alfabéticos: 0=Ana 1=Carlos 2=Elena 3=Juan 4=Luis 5=María.
    private static readonly Dictionary<DayOfWeek, (int h, int m, int srv, int cli)[]> Patron = new()
    {
        [DayOfWeek.Monday]    = new[] { (9,  0, 1, 0), (11,  0, 0, 3), (14,  0, 2, 1) },
        [DayOfWeek.Tuesday]   = new[] { (9, 30, 0, 4), (11,  0, 1, 2), (14, 30, 2, 5) },
        [DayOfWeek.Wednesday] = new[] { (9,  0, 2, 1), (11, 30, 0, 4), (15,  0, 1, 0) },
        [DayOfWeek.Thursday]  = new[] { (9,  0, 1, 3), (11,  0, 0, 5), (15, 30, 2, 2) },
        [DayOfWeek.Friday]    = new[] { (9, 30, 0, 0), (11,  0, 2, 2), (14, 30, 1, 4) },
    };

    /// <summary>Email demo único: "adrian.sosa" + sufijo + "@zenmassage.com".</summary>
    private static string Email(string local, string suffix) => $"{local}{suffix}@zenmassage.com";

    /// <summary>Devuelve el lunes de la semana que contiene 'fecha' (Kind=Unspecified).</summary>
    public static DateTime LunesDeLaSemana(DateTime fecha)
    {
        var d = fecha.Date;
        int diff = ((int)d.DayOfWeek + 6) % 7; // Monday=0
        return DateTime.SpecifyKind(d.AddDays(-diff), DateTimeKind.Unspecified);
    }

    /// <summary>
    /// Agrega todas las entidades del dataset al contexto y devuelve la Sucursal creada.
    /// El caller debe llamar SaveChanges luego.
    /// </summary>
    public static Sucursal Build(AppDbContext db, Opciones opts)
    {
        var sucursal = new Sucursal
        {
            Id            = Guid.NewGuid(),
            Nombre        = "Zen Massage Center",
            Slug          = opts.Slug,
            Slogan        = "Tu momento de paz y relajación",
            Direccion     = "Av. Siempre Viva 742, Springfield",
            Telefono      = "123456789",
            Email         = Email("contacto", opts.EmailSuffix),
            Moneda        = "ARS",
            Rubros        = new List<string> { "Masajes", "Bienestar", "Spa" },
            FechaCreacion = DateTime.UtcNow,
            EsDemo        = opts.EsDemo,
            DemoExpiraEn  = opts.DemoExpiraEn,
        };
        db.Sucursales.Add(sucursal);

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

        var caja = new Caja
        {
            Id         = Guid.NewGuid(),
            SucursalId = sucursal.Id,
            Nombre     = "Caja Principal",
            Activa     = true
        };
        db.Cajas.Add(caja);

        // Owner / Profesional (Adrián)
        var owner = new Usuario
        {
            Id            = Guid.NewGuid(),
            Nombre        = "Adrián",
            Apellido      = "Sosa",
            Email         = Email("adrian.sosa", opts.EmailSuffix),
            Telefono      = "987654321",
            PasswordHash  = BCrypt.Net.BCrypt.HashPassword("Demo123!"),
            FechaCreacion = DateTime.UtcNow
        };
        db.Usuarios.Add(owner);

        var rolOwner = new UsuarioSucursalRol
        {
            Id            = Guid.NewGuid(),
            UsuarioId     = owner.Id,
            SucursalId    = sucursal.Id,
            Rol           = RolUsuario.Profesional,
            EsOwner       = true,
            Estado        = EstadoUsuario.Activo,
            FechaCreacion = DateTime.UtcNow
        };
        db.UsuarioSucursalRoles.Add(rolOwner);
        AgregarHorarios(db, rolOwner.Id);

        // Servicios [0]=45m [1]=60m [2]=90m
        var s0 = NuevoServicio(sucursal.Id, "Masaje Relajante", "Masaje suave para reducir el estrés.", 4000, 45);
        var s1 = NuevoServicio(sucursal.Id, "Masaje Descontracturante", "Masaje profundo para liberar tensiones musculares.", 5000, 60);
        var s2 = NuevoServicio(sucursal.Id, "Masaje con Piedras Calientes", "Terapia geotermal para relajación profunda.", 7500, 90);
        db.Servicios.AddRange(s0, s1, s2);
        var servicios = new List<Servicio> { s0, s1, s2 };
        foreach (var srv in servicios)
            db.UsuarioServicios.Add(new UsuarioServicio { UsuarioSucursalRolId = rolOwner.Id, ServicioId = srv.Id });

        // Clientes (orden alfabético: Ana, Carlos, Elena, Juan, Luis, María)
        var clientes = new List<Cliente>
        {
            NuevoCliente(sucursal.Id, "Ana",    "Martínez",  "1166778899", "ana.mtz@example.com"),
            NuevoCliente(sucursal.Id, "Carlos", "Rodríguez", "1155443322", "carlos.rod@example.com"),
            NuevoCliente(sucursal.Id, "Elena",  "Sánchez",   "1144556677", "elena.s@example.com"),
            NuevoCliente(sucursal.Id, "Juan",   "Pérez",     "1122334455", "juan.perez@example.com"),
            NuevoCliente(sucursal.Id, "Luis",   "González",  "1133221100", "luis.gonz@example.com"),
            NuevoCliente(sucursal.Id, "María",  "García",    "1199887766", "maria.garcia@example.com"),
        };
        db.Clientes.AddRange(clientes);

        var sesion = new CajaSesion
        {
            Id                = Guid.NewGuid(),
            CajaId            = caja.Id,
            UsuarioAperturaId = owner.Id,
            FechaApertura     = DateTime.SpecifyKind(opts.SemanaBase.AddHours(9), DateTimeKind.Unspecified),
            MontoInicial      = 0,
            Estado            = EstadoCajaSesion.Abierta
        };
        db.CajaSesiones.Add(sesion);

        // Usuarios extra: Martina (Profesional), Diego (Caja).
        // Owner (Adrián) ya cubre el acceso total; Administrativo sería redundante
        // (misma autoridad), así la demo expone 3 roles bien diferenciados.
        AgregarUsuarioExtra(db, sucursal.Id, servicios, opts.EmailSuffix,
            "martina.lopez", "Martina", "López", RolUsuario.Profesional);
        AgregarUsuarioExtra(db, sucursal.Id, servicios, opts.EmailSuffix,
            "diego.fernandez", "Diego", "Fernández", RolUsuario.Caja);

        CrearTurnos(db, sucursal.Id, owner.Id, caja.Id, sesion.Id, servicios, clientes, opts.SemanaBase);

        return sucursal;
    }

    private static void AgregarHorarios(AppDbContext db, Guid rolId)
    {
        for (int i = 1; i <= 5; i++)
            db.HorariosLaborales.Add(new HorarioLaboral
            {
                Id                   = Guid.NewGuid(),
                UsuarioSucursalRolId = rolId,
                DiaSemana            = (DayOfWeek)i,
                HoraDesde            = new TimeSpan(9, 0, 0),
                HoraHasta            = new TimeSpan(18, 0, 0),
                Activo               = true
            });
    }

    private static Servicio NuevoServicio(Guid sucId, string nombre, string desc, decimal precio, int min) => new()
    {
        Id                  = Guid.NewGuid(),
        SucursalId          = sucId,
        Nombre              = nombre,
        Descripcion         = desc,
        Precio              = precio,
        DuracionClienteMin  = min,
        DuracionAgendaMin   = min,
        Activo              = true,
        ModalidadPrestacion = ModalidadPrestacion.Presencial,
        TipoServicio        = TipoServicio.Regular
    };

    private static Cliente NuevoCliente(Guid sucId, string nombre, string ape, string tel, string email) => new()
    {
        Id = Guid.NewGuid(), SucursalId = sucId, Nombre = nombre, Apellido = ape, Telefono = tel, Email = email
    };

    private static void AgregarUsuarioExtra(
        AppDbContext db, Guid sucId, List<Servicio> servicios, string suffix,
        string emailLocal, string nombre, string apellido, RolUsuario rolTipo)
    {
        var usuario = new Usuario
        {
            Id            = Guid.NewGuid(),
            Nombre        = nombre,
            Apellido      = apellido,
            Email         = Email(emailLocal, suffix),
            Telefono      = "000000000",
            PasswordHash  = BCrypt.Net.BCrypt.HashPassword("Demo123!"),
            FechaCreacion = DateTime.UtcNow
        };
        db.Usuarios.Add(usuario);

        var rol = new UsuarioSucursalRol
        {
            Id            = Guid.NewGuid(),
            UsuarioId     = usuario.Id,
            SucursalId    = sucId,
            Rol           = rolTipo,
            EsOwner       = false,
            Estado        = EstadoUsuario.Activo,
            FechaCreacion = DateTime.UtcNow
        };
        db.UsuarioSucursalRoles.Add(rol);
        AgregarHorarios(db, rol.Id);

        if (rolTipo == RolUsuario.Profesional)
        {
            foreach (var p in new[] { PermisoSistema.AgendarTurnos, PermisoSistema.EditarTurnos, PermisoSistema.VerTelefonoClientes })
                db.UsuariosPermisos.Add(new UsuarioPermiso { Id = Guid.NewGuid(), UsuarioSucursalRolId = rol.Id, Permiso = p });
            foreach (var srv in servicios)
                db.UsuarioServicios.Add(new UsuarioServicio { UsuarioSucursalRolId = rol.Id, ServicioId = srv.Id });
        }
        else if (rolTipo == RolUsuario.Caja)
        {
            db.UsuariosPermisos.Add(new UsuarioPermiso { Id = Guid.NewGuid(), UsuarioSucursalRolId = rol.Id, Permiso = PermisoSistema.CargarCobranzas });
        }
        // Administrativo: acceso completo implícito por rol, sin permisos explícitos.
    }

    private static void CrearTurnos(
        AppDbContext db, Guid sucId, Guid profId, Guid cajaId, Guid sesionId,
        List<Servicio> servicios, List<Cliente> clientes, DateTime semanaBase)
    {
        // Poblar 2 semanas: la anterior (completados) y la actual (confirmados), para que
        // la agenda demo se vea "viva" alrededor de hoy.
        for (int semana = -1; semana <= 0; semana++)
        {
            for (int dia = 0; dia < 5; dia++)
            {
                var fecha = semanaBase.AddDays(semana * 7 + dia); // Lun..Vie
                if (!Patron.TryGetValue(fecha.DayOfWeek, out var slots)) continue;

                foreach (var (h, m, srvIdx, cliIdx) in slots)
                {
                    var servicio = servicios[srvIdx % servicios.Count];
                    var cliente  = clientes[(cliIdx + (semana + 1)) % clientes.Count];

                    // DateTimeKind.Unspecified → se serializa SIN 'Z' → el browser lo muestra tal cual.
                    var inicio = DateTime.SpecifyKind(fecha.AddHours(h).AddMinutes(m), DateTimeKind.Unspecified);
                    var fin    = inicio.AddMinutes(servicio.DuracionAgendaMin);
                    var estado = semana < 0 ? EstadoTurno.Completado : EstadoTurno.Confirmado;

                    var turno = new Turno
                    {
                        Id              = Guid.NewGuid(),
                        SucursalId      = sucId,
                        ClienteId       = cliente.Id,
                        ServicioId      = servicio.Id,
                        ProfesionalId   = profId,
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
                            UsuarioId      = profId,
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
}
