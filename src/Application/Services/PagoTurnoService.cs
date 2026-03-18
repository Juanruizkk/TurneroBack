using Microsoft.Extensions.Logging;
using TurneroApp.Application.DTOs.Pagos;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.Services;

public sealed class PagoTurnoService : IPagoTurnoService
{
    private readonly ITurnoRepository         _turnoRepo;
    private readonly ICajaRepository          _cajaRepo;
    private readonly ISucursalRepository      _sucursalRepo;
    private readonly IMercadoPagoGateway      _mpGateway;
    private readonly IEmailService            _emailService;
    private readonly ILogger<PagoTurnoService> _logger;
    private readonly string                   _frontendUrl;

    public PagoTurnoService(
        ITurnoRepository          turnoRepo,
        ICajaRepository           cajaRepo,
        ISucursalRepository       sucursalRepo,
        IMercadoPagoGateway       mpGateway,
        IEmailService             emailService,
        ILogger<PagoTurnoService> logger,
        string                    frontendUrl)
    {
        _turnoRepo    = turnoRepo;
        _cajaRepo     = cajaRepo;
        _sucursalRepo = sucursalRepo;
        _mpGateway    = mpGateway;
        _emailService = emailService;
        _logger       = logger;
        _frontendUrl  = frontendUrl;
    }

    /// <inheritdoc/>
    public async Task<string> GenerarLinkDePagoAsync(Guid turnoId)
    {
        var turno = await _turnoRepo.GetByIdConRelacionesAsync(turnoId)
            ?? throw new InvalidOperationException($"Turno {turnoId} no encontrado.");

        if (turno.Estado != EstadoTurno.PendientePago)
            throw new InvalidOperationException(
                $"El turno no está en estado PendientePago (estado actual: {turno.Estado}).");

        // Si ya tiene una preferencia generada, la reutilizamos (idempotencia)
        if (!string.IsNullOrEmpty(turno.MpPreferenceId))
            return turno.MpPreferenceId;

        var monto  = turno.MontoSena > 0 ? turno.MontoSena!.Value : turno.PrecioServicio;
        var titulo = $"Seña — {turno.Servicio?.Nombre ?? "Servicio"}";

        var preferenceId = await _mpGateway.CrearPreferenciaAsync(
            turnoId.ToString(),
            titulo,
            monto,
            _frontendUrl);

        turno.MpPreferenceId = preferenceId;
        _turnoRepo.Update(turno);
        await _turnoRepo.SaveChangesAsync();

        return preferenceId;
    }

    /// <inheritdoc/>
    public async Task ProcesarNotificacionPagoAsync(long paymentId)
    {
        PaymentDetailsDto pago;
        try
        {
            pago = await _mpGateway.ObtenerDetallesPagoAsync(paymentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error consultando pago {PaymentId} a MP.", paymentId);
            throw;
        }

        _logger.LogInformation(
            "Notificación MP: PaymentId={PaymentId}, Status={Status}, ExternalRef={ExternalRef}",
            pago.PaymentId, pago.Status, pago.ExternalReference);

        if (!Guid.TryParse(pago.ExternalReference, out var turnoId))
        {
            _logger.LogWarning("ExternalReference inválido: '{Ref}'.", pago.ExternalReference);
            return;
        }

        var turno = await _turnoRepo.GetByIdConRelacionesAsync(turnoId);
        if (turno is null)
        {
            _logger.LogWarning("Turno {TurnoId} no encontrado para pago {PaymentId}.", turnoId, paymentId);
            return;
        }

        // Pago rechazado o cancelado → marcar con estado propio (no confundir con cancelación voluntaria)
        if (pago.Status is "rejected" or "cancelled")
        {
            if (turno.Estado == EstadoTurno.PendientePago)
            {
                turno.Estado = EstadoTurno.PagoRechazado;
                _turnoRepo.Update(turno);
                await _turnoRepo.SaveChangesAsync();
                _logger.LogInformation("Turno {TurnoId} marcado como PagoRechazado (status={Status}).", turnoId, pago.Status);
            }
            return;
        }

        if (pago.Status != "approved")
        {
            _logger.LogInformation("Pago {PaymentId} ignorado (status={Status}).", paymentId, pago.Status);
            return;
        }

        // Idempotencia: si ya está confirmado, no hacer nada
        if (turno.Estado == EstadoTurno.Confirmado)
        {
            _logger.LogInformation("Turno {TurnoId} ya estaba Confirmado. Webhook ignorado.", turnoId);
            return;
        }

        turno.Estado       = EstadoTurno.Confirmado;
        turno.TotalCobrado += pago.Amount;

        var caja = await _cajaRepo.GetBySucursalAsync(turno.SucursalId);
        if (caja is null)
        {
            _logger.LogError("No existe Caja para sucursal {SucursalId}.", turno.SucursalId);
            // Confirmamos el turno igual, pero sin movimiento de caja
            _turnoRepo.Update(turno);
            await _turnoRepo.SaveChangesAsync();
            return;
        }

        var movimiento = new MovimientoCaja
        {
            Id             = Guid.NewGuid(),
            CajaId         = caja.Id,
            TurnoId        = turno.Id,
            UsuarioId      = null,   // sistema/webhook — sin usuario humano
            TipoMovimiento = TipoMovimientoCaja.CobroTurno,
            MedioPago      = MedioPago.MercadoPago,
            Monto          = pago.Amount,
            Observaciones  = $"Seña cobrada automáticamente vía Mercado Pago (PaymentId: {paymentId})",
            Fecha          = DateTime.UtcNow,
        };

        var pagoTurno = new PagoTurno
        {
            Id               = Guid.NewGuid(),
            TurnoId          = turno.Id,
            MovimientoCajaId = movimiento.Id,
            Concepto         = ConceptoPagoTurno.Sena,
            Monto            = pago.Amount,
            Fecha            = movimiento.Fecha,
        };

        await _cajaRepo.RegistrarMovimientoTransaccionalAsync(movimiento, pagoTurno, turno);

        _logger.LogInformation(
            "Turno {TurnoId} confirmado y seña de {Monto} registrada en Caja.", turnoId, pago.Amount);

        // Enviar email de confirmación al cliente
        var clienteEmail = turno.Cliente?.Email;
        if (!string.IsNullOrWhiteSpace(clienteEmail))
        {
            try
            {
                var sucursal         = await _sucursalRepo.GetByIdAsync(turno.SucursalId);
                var sucursalNombre   = sucursal?.Nombre ?? "TurneroApp";
                var clienteNombre    = $"{turno.Cliente!.Nombre} {turno.Cliente.Apellido}".Trim();
                var servicioNombre   = turno.Servicio?.Nombre ?? string.Empty;
                var profesionalNombre = turno.Profesional is not null
                    ? $"{turno.Profesional.Nombre} {turno.Profesional.Apellido}".Trim()
                    : string.Empty;
                var cancelUrl = $"{_frontendUrl.TrimEnd('/')}/turno/{turno.TokenGestion}";

                await _emailService.EnviarConfirmacionTurnoAsync(
                    clienteEmail,
                    clienteNombre,
                    sucursalNombre,
                    servicioNombre,
                    profesionalNombre,
                    turno.FechaHoraInicio,
                    turno.FechaHoraFin,
                    cancelUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo enviar el email de confirmación para el turno {TurnoId}.", turnoId);
            }
        }
    }
}
