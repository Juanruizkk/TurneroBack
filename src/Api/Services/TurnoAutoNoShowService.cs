using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Enums;

namespace TurneroApp.Api.Services;

/// <summary>
/// BackgroundService que se ejecuta diariamente a las 3:00 AM y marca como NoShow
/// los turnos Confirmados o Pendientes cuya FechaHoraFin sea anterior a la medianoche de hoy.
/// </summary>
public class TurnoAutoNoShowService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TurnoAutoNoShowService> _logger;

    public TurnoAutoNoShowService(
        IServiceScopeFactory scopeFactory,
        ILogger<TurnoAutoNoShowService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TurnoAutoNoShowService iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var ahora = DateTime.Now;
            var proximas3AM = ahora.Date.AddDays(ahora.Hour < 3 ? 0 : 1).AddHours(3);
            var espera = proximas3AM - ahora;

            _logger.LogInformation(
                "TurnoAutoNoShowService: próxima ejecución en {Espera:hh\\:mm\\:ss} (a las {Hora}).",
                espera, proximas3AM.ToString("dd/MM/yyyy HH:mm"));

            await Task.Delay(espera, stoppingToken);

            if (stoppingToken.IsCancellationRequested) break;

            await EjecutarAsync(stoppingToken);

        }
    }

    internal async Task EjecutarAsync(CancellationToken ct)
    {
        _logger.LogInformation("TurnoAutoNoShowService: iniciando marcado de NoShow.");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ITurnoRepository>();

            // Todos los turnos Confirmado/Pendiente con FechaHoraFin antes de hoy a las 00:00
            var fechaLimite = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0, DateTimeKind.Unspecified);
            var turnos = (await repo.GetConfirmadosPendientesAntesDeAsync(fechaLimite)).ToList();

            if (turnos.Count == 0)
            {
                _logger.LogInformation("TurnoAutoNoShowService: no hay turnos para marcar.");
                return;
            }

            foreach (var turno in turnos)
                turno.Estado = EstadoTurno.NoShow;

            repo.UpdateRange(turnos);
            await repo.SaveChangesAsync();

            _logger.LogInformation(
                "TurnoAutoNoShowService: {Count} turno(s) marcados como NoShow.", turnos.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TurnoAutoNoShowService: error durante la ejecución.");
        }
    }
}
