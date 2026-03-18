using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Enums;

namespace TurneroApp.Api.Services;

public class TurnoPagoExpirationService : BackgroundService
{
    private const int IntervalMinutos = 15;
    private const int TimeoutMinutos  = 30;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TurnoPagoExpirationService> _logger;

    public TurnoPagoExpirationService(
        IServiceScopeFactory scopeFactory,
        ILogger<TurnoPagoExpirationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(IntervalMinutos), stoppingToken);

            try
            {
                await ExpirarTurnosAbandonadosAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al expirar turnos PendientePago abandonados.");
            }
        }
    }

    internal async Task ExpirarTurnosAbandonadosAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITurnoRepository>();

        var limite   = DateTime.Now.AddMinutes(-TimeoutMinutos);
        var vencidos = (await repo.GetPendientePagoVencidosAsync(limite)).ToList();

        if (vencidos.Count == 0) return;

        foreach (var turno in vencidos)
            turno.Estado = EstadoTurno.PagoRechazado;

        repo.UpdateRange(vencidos);
        await repo.SaveChangesAsync();

        _logger.LogInformation(
            "TurnoPagoExpirationService: {Count} turno(s) PendientePago expirados → PagoRechazado.",
            vencidos.Count);
    }
}
