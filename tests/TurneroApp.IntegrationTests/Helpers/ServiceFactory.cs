using Moq;
using TurneroApp.Application.Interfaces;
using TurneroApp.Application.Services;
using TurneroApp.Infrastructure.Data;
using TurneroApp.Infrastructure.Repositories;

namespace TurneroApp.IntegrationTests.Helpers;

internal static class ServiceFactory
{
    public static PublicoService BuildPublicoService(AppDbContext db)
    {
        return new PublicoService(
            new SucursalRepository(db),
            new ServicioRepository(db),
            new TurnoRepository(db),
            new UsuarioSucursalRolRepository(db),
            new HorarioLaboralRepository(db),
            new UsuarioServicioRepository(db),
            new ClienteRepository(db),
            BuildTurnoService(db));
    }

    public static TurnoService BuildTurnoService(AppDbContext db)
    {
        var turnoRepo       = new TurnoRepository(db);
        var servicioRepo    = new ServicioRepository(db);
        var clienteRepo     = new ClienteRepository(db);
        var sucursalRepo    = new SucursalRepository(db);
        var comentarioRepo  = new ComentarioTurnoRepository(db);
        var suscripcionRepo = new SuscripcionRepository(db);
        var guard           = new SuscripcionGuard(suscripcionRepo);

        return new TurnoService(
            turnoRepo, servicioRepo, clienteRepo, sucursalRepo,
            Mock.Of<IEmailService>(), comentarioRepo,
            "http://localhost",
            notificaciones:   Mock.Of<ITurnoNotificacionService>(),
            suscripcionRepo:  suscripcionRepo,
            suscripcionGuard: guard
            // horarioRepo y usuarioServicioRepo omitidos intencionalmente:
            // evita validar horario laboral y asignación de servicios del profesional
        );
    }
}
