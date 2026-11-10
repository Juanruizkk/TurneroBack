using Microsoft.Extensions.Options;
using TurneroApp.Api.Options;
using TurneroApp.Application.Interfaces;

namespace TurneroApp.Api.Services;

/// <summary>
/// BackgroundService que cada N minutos hace teardown de las sucursales demo vencidas
/// (EsDemo && DemoExpiraEn &lt; now). Patrón tomado de RecordatoriosEmailBackgroundService.
/// </summary>
public class DemoCleanupBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DemoCleanupBackgroundService> _logger;
    private readonly TimeSpan _intervalo;

    public DemoCleanupBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<DemoCleanupBackgroundService> logger,
        IOptions<DemoOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
        _intervalo    = TimeSpan.FromMinutes(Math.Max(1, options.Value.LimpiezaCadaMinutos));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "DemoCleanupBackgroundService iniciado. Intervalo: {Min} min.", _intervalo.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IDemoTenantService>();
                var n = await svc.LimpiarVencidasAsync(stoppingToken);
                if (n > 0)
                    _logger.LogInformation("DemoCleanup: {N} demo(s) vencida(s) eliminada(s).", n);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DemoCleanup: error en el ciclo de limpieza.");
            }

            try
            {
                await Task.Delay(_intervalo, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
