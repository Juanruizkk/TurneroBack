using System.Text.Json;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;
using TurneroApp.Infrastructure.Data;

namespace TurneroApp.IntegrationTests.Helpers;

internal static class SeedHelper
{
    // ─── Horario JSON helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Genera un HorariosAtencionJson con una única franja para el día indicado.
    /// Los demás días quedan cerrados (abierto: false).
    /// </summary>
    public static string HorarioJson(string diaNombre, string apertura, string cierre)
    {
        var payload = new Dictionary<string, object>
        {
            [diaNombre] = new
            {
                Abierto = true,
                Franjas = new[] { new { Apertura = apertura, Cierre = cierre } },
            },
        };
        return JsonSerializer.Serialize(payload);
    }

    // ─── ConfiguracionSucursal ────────────────────────────────────────────────

    public static async Task<ConfiguracionSucursal> SeedConfiguracionAsync(
        AppDbContext db, Guid sucursalId,
        string horariosJson = "{}",
        int buffer = 0,
        int anticipacionMin = 0)
    {
        var c = new ConfiguracionSucursal
        {
            Id            = Guid.NewGuid(),
            SucursalId    = sucursalId,
            HorariosAtencionJson                = horariosJson,
            MinutosBufferEntreTurnos            = buffer,
            MinutosAnticipacionMinimaReserva    = anticipacionMin,
            DiasMaximosReservaFutura            = 365,
            ModoConfirmacionTurnos              = ModoConfirmacionTurnos.Automatico,
        };
        db.ConfiguracionesSucursal.Add(c);
        await db.SaveChangesAsync();
        return c;
    }

    // ─── UsuarioSucursalRol ───────────────────────────────────────────────────

    public static async Task<UsuarioSucursalRol> SeedUsuarioSucursalRolAsync(
        AppDbContext db, Guid sucursalId, Guid usuarioId,
        RolUsuario rol = RolUsuario.Profesional,
        EstadoUsuario estado = EstadoUsuario.Activo)
    {
        var usr = new UsuarioSucursalRol
        {
            Id          = Guid.NewGuid(),
            SucursalId  = sucursalId,
            UsuarioId   = usuarioId,
            Rol         = rol,
            Estado      = estado,
            EsOwner     = false,
            FechaCreacion = DateTime.UtcNow,
        };
        db.UsuarioSucursalRoles.Add(usr);
        await db.SaveChangesAsync();
        return usr;
    }

    // ─── HorarioLaboral ───────────────────────────────────────────────────────

    public static async Task<HorarioLaboral> SeedHorarioLaboralAsync(
        AppDbContext db, Guid usuarioSucursalRolId,
        DayOfWeek dia, TimeSpan desde, TimeSpan hasta)
    {
        var h = new HorarioLaboral
        {
            Id                  = Guid.NewGuid(),
            UsuarioSucursalRolId = usuarioSucursalRolId,
            DiaSemana           = dia,
            HoraDesde           = desde,
            HoraHasta           = hasta,
            Activo              = true,
        };
        db.HorariosLaborales.Add(h);
        await db.SaveChangesAsync();
        return h;
    }
    public static async Task<Plan> SeedPlanAsync(
        AppDbContext db, int? maxTurnosMes = null,
        int maxUsuarios = 10, int? maxServicios = null)
    {
        var plan = new Plan
        {
            Id = Guid.NewGuid(), Nombre = "Plan Test",
            MaxTurnosMes = maxTurnosMes, MaxUsuarios = maxUsuarios,
            MaxServicios = maxServicios, PrecioMensual = 0, EsActivo = true,
        };
        db.Planes.Add(plan);
        await db.SaveChangesAsync();
        return plan;
    }

    public static async Task<Sucursal> SeedSucursalAsync(AppDbContext db)
    {
        var s = new Sucursal
        {
            Id = Guid.NewGuid(), Nombre = "Sucursal Test",
            Slug = $"test-{Guid.NewGuid():N}", Moneda = "ARS",
            Direccion = string.Empty, Telefono = string.Empty, Email = string.Empty,
            Activa = true, FechaCreacion = DateTime.UtcNow,
        };
        db.Sucursales.Add(s);
        await db.SaveChangesAsync();
        return s;
    }

    public static async Task<SucursalSuscripcion> SeedSuscripcionAsync(
        AppDbContext db, Guid sucursalId, Guid planId,
        EstadoSuscripcion estado = EstadoSuscripcion.PruebActiva, int diasVencimiento = 30)
    {
        var sus = new SucursalSuscripcion
        {
            Id = Guid.NewGuid(), SucursalId = sucursalId, PlanId = planId,
            Estado = estado, FechaInicio = DateTime.UtcNow.AddDays(-1),
            FechaVencimiento = DateTime.UtcNow.AddDays(diasVencimiento),
            EnPeriodoPrueba = estado == EstadoSuscripcion.PruebActiva,
        };
        db.SucursalSuscripciones.Add(sus);
        await db.SaveChangesAsync();
        return sus;
    }

    public static async Task<Cliente> SeedClienteAsync(AppDbContext db, Guid sucursalId)
    {
        var c = new Cliente
        {
            Id = Guid.NewGuid(), SucursalId = sucursalId,
            Nombre = "Juan", Apellido = "Test", Telefono = "1111111111",
        };
        db.Clientes.Add(c);
        await db.SaveChangesAsync();
        return c;
    }

    public static async Task<Servicio> SeedServicioAsync(
        AppDbContext db, Guid sucursalId, int duracionAgendaMin = 30)
    {
        var s = new Servicio
        {
            Id = Guid.NewGuid(), SucursalId = sucursalId,
            Nombre = "Corte", Descripcion = string.Empty, Aclaraciones = string.Empty,
            Precio = 5000m, DuracionClienteMin = duracionAgendaMin,
            DuracionAgendaMin = duracionAgendaMin, RequiereSena = false, Activo = true,
            ModalidadPrestacion = ModalidadPrestacion.Presencial,
            TipoServicio = TipoServicio.Regular,
        };
        db.Servicios.Add(s);
        await db.SaveChangesAsync();
        return s;
    }

    public static async Task<Usuario> SeedProfesionalAsync(AppDbContext db)
    {
        // ProfesionalId en Turno tiene FK Restrict a usuarios.Id,
        // por eso necesitamos un Usuario real en la DB.
        var u = new Usuario
        {
            Id = Guid.NewGuid(), Nombre = "Prof", Apellido = "Test",
            Email = $"{Guid.NewGuid():N}@test.com", Telefono = "2222222222",
            PasswordHash = "x", FechaCreacion = DateTime.UtcNow,
        };
        db.Usuarios.Add(u);
        await db.SaveChangesAsync();
        return u;
    }

    public static async Task<Turno> SeedTurnoAsync(
        AppDbContext db, Guid sucursalId, Guid clienteId,
        Guid servicioId, Guid profesionalId,
        DateTime fechaHoraInicio, int duracionMin = 30)
    {
        var t = new Turno
        {
            Id = Guid.NewGuid(), SucursalId = sucursalId,
            ClienteId = clienteId, ServicioId = servicioId, ProfesionalId = profesionalId,
            FechaHoraInicio = fechaHoraInicio,
            FechaHoraFin    = fechaHoraInicio.AddMinutes(duracionMin),
            Estado = EstadoTurno.Confirmado, Origen = OrigenTurno.Administrativo,
            TokenGestion = Guid.NewGuid().ToString("N"),
            PrecioServicio = 5000m, TotalCobrado = 0,
            FechaCreacion = DateTime.UtcNow,
        };
        db.Turnos.Add(t);
        await db.SaveChangesAsync();
        return t;
    }
}
