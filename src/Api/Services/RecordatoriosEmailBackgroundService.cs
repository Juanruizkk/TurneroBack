using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;

namespace TurneroApp.Api.Services;

/// <summary>
/// BackgroundService que se ejecuta cada 15 minutos y envía recordatorios por email
/// a los clientes con turnos próximos.
///
/// Lógica de ventanas:
///   - 3 h : FechaHoraInicio ∈ (ahora, ahora+3h]  y Recordatorio3hEnviado == false
///   - 24 h: FechaHoraInicio ∈ (ahora, ahora+24h] y Recordatorio24hEnviado == false
///
/// Si un turno cae en ambas ventanas simultáneamente (creado con menos de 3 h de antelación)
/// solo se envía el recordatorio de 3 h y se marcan ambos flags como enviados para evitar
/// spam al cliente.
/// </summary>
public class RecordatoriosEmailBackgroundService : BackgroundService
{   
    /* Para test usar esto */
    /* private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(10); */
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecordatoriosEmailBackgroundService> _logger;
    private readonly string _frontendUrl;

    public RecordatoriosEmailBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<RecordatoriosEmailBackgroundService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
        _frontendUrl  = configuration["App:FrontendUrl"] ?? "http://localhost:5173";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RecordatoriosEmailBackgroundService iniciado. Intervalo: {Intervalo} min.", Intervalo.TotalMinutes);

        // Primera ejecución al arrancar, luego cada Intervalo
        while (!stoppingToken.IsCancellationRequested)
        {
            await EjecutarAsync(stoppingToken);
            await Task.Delay(Intervalo, stoppingToken);
        }
    }

    private async Task EjecutarAsync(CancellationToken ct)
    {
        _logger.LogDebug("RecordatoriosEmailBackgroundService: iniciando ciclo.");

        try
        {
            using var scope        = _scopeFactory.CreateScope();
            var repo               = scope.ServiceProvider.GetRequiredService<ITurnoRepository>();
            var emailService       = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var ahora     = DateTime.Now;
            var limite3h  = ahora.AddHours(3);
            var limite24h = ahora.AddHours(24);

            var turnos = (await repo.GetParaRecordatoriosAsync(ahora, limite24h, limite3h)).ToList();

            if (turnos.Count == 0)
            {
                _logger.LogDebug("RecordatoriosEmailBackgroundService: sin recordatorios pendientes.");
                return;
            }

            _logger.LogInformation(
                "RecordatoriosEmailBackgroundService: {Count} turno(s) a procesar.", turnos.Count);

            var modificados = new List<Turno>();

            foreach (var turno in turnos)
            {
                // Sin email → saltar
                if (string.IsNullOrWhiteSpace(turno.Cliente?.Email))
                    continue;

                var permiteCancelacion = turno.Sucursal?.ConfiguracionSucursal?.PermiteCancelacionCliente ?? true;
                var cancelUrl     = permiteCancelacion ? $"{_frontendUrl}/turno/{turno.TokenGestion}" : string.Empty;
                var sucursalNombre = turno.Sucursal?.Nombre ?? string.Empty;
                var clienteNombre  = $"{turno.Cliente.Nombre} {turno.Cliente.Apellido}".Trim();
                var profesionalNombre = $"{turno.Profesional?.Nombre} {turno.Profesional?.Apellido}".Trim();

                // ── Ventana de 3 h (prioridad alta) ────────────────────────────────
                if (!turno.Recordatorio3hEnviado && turno.FechaHoraInicio <= limite3h)
                {
                    try
                    {
                        await emailService.EnviarRecordatorioAsync(
                            turno.Cliente.Email,
                            clienteNombre,
                            sucursalNombre,
                            turno.Servicio.Nombre,
                            profesionalNombre,
                            turno.FechaHoraInicio,
                            turno.FechaHoraFin,
                            cancelUrl,
                            es24h: false);

                        turno.Recordatorio3hEnviado = true;

                        // Si tampoco se había enviado el de 24 h, marcarlo también para
                        // no enviar un segundo email confuso en el mismo ciclo o el siguiente.
                        turno.Recordatorio24hEnviado = true;

                        modificados.Add(turno);

                        _logger.LogInformation(
                            "RecordatoriosEmailBackgroundService: recordatorio 3 h enviado → turno {Id} ({Cliente}).",
                            turno.Id, clienteNombre);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "RecordatoriosEmailBackgroundService: error al enviar recordatorio 3 h al turno {Id}.", turno.Id);
                    }

                    continue; // Si cae en 3 h, no procesar también el de 24 h
                }

                // ── Ventana de 24 h ─────────────────────────────────────────────────
                if (!turno.Recordatorio24hEnviado && turno.FechaHoraInicio <= limite24h)
                {
                    try
                    {
                        await emailService.EnviarRecordatorioAsync(
                            turno.Cliente.Email,
                            clienteNombre,
                            sucursalNombre,
                            turno.Servicio.Nombre,
                            profesionalNombre,
                            turno.FechaHoraInicio,
                            turno.FechaHoraFin,
                            cancelUrl,
                            es24h: true);

                        turno.Recordatorio24hEnviado = true;
                        modificados.Add(turno);

                        _logger.LogInformation(
                            "RecordatoriosEmailBackgroundService: recordatorio 24 h enviado → turno {Id} ({Cliente}).",
                            turno.Id, clienteNombre);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "RecordatoriosEmailBackgroundService: error al enviar recordatorio 24 h al turno {Id}.", turno.Id);
                    }
                }
            }

            if (modificados.Count > 0)
            {
                repo.UpdateRange(modificados);
                await repo.SaveChangesAsync();

                _logger.LogInformation(
                    "RecordatoriosEmailBackgroundService: {Count} flag(s) de recordatorio guardados.", modificados.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RecordatoriosEmailBackgroundService: error en el ciclo de ejecución.");
        }
    }
}
