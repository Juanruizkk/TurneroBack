using Microsoft.Extensions.Logging;
using TurneroApp.Application.DTOs.Pagos;
using TurneroApp.Application.DTOs.Turno;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.Services;

public sealed class PagoTurnoService : IPagoTurnoService
{
    private readonly ITurnoRepository           _turnoRepo;
    private readonly ICajaRepository            _cajaRepo;
    private readonly ISucursalRepository        _sucursalRepo;
    private readonly IMercadoPagoGateway        _mpGateway;
    private readonly IMercadoPagoOAuthService   _oauthService;
    private readonly IEmailService              _emailService;
    private readonly ILogger<PagoTurnoService>  _logger;
    private readonly string                     _frontendUrl;
    private readonly string                     _backendUrl;
    private readonly ITurnoNotificacionService? _notificaciones;

    public PagoTurnoService(
        ITurnoRepository           turnoRepo,
        ICajaRepository            cajaRepo,
        ISucursalRepository        sucursalRepo,
        IMercadoPagoGateway        mpGateway,
        IMercadoPagoOAuthService   oauthService,
        IEmailService              emailService,
        ILogger<PagoTurnoService>  logger,
        string                     frontendUrl,
        string                     backendUrl,
        ITurnoNotificacionService? notificaciones = null)
    {
        _turnoRepo      = turnoRepo;
        _cajaRepo       = cajaRepo;
        _sucursalRepo   = sucursalRepo;
        _mpGateway      = mpGateway;
        _oauthService   = oauthService;
        _emailService   = emailService;
        _logger         = logger;
        _frontendUrl    = frontendUrl;
        _backendUrl     = backendUrl;
        _notificaciones = notificaciones;
    }

    /// <inheritdoc/>
    public async Task<string> GenerarLinkDePagoAsync(Guid turnoId)
    {
        var turno = await _turnoRepo.GetByIdConRelacionesAsync(turnoId)
            ?? throw new InvalidOperationException($"Turno {turnoId} no encontrado.");

        if (turno.Estado != EstadoTurno.PendientePago)
            throw new InvalidOperationException(
                $"El turno no está en estado PendientePago (estado actual: {turno.Estado}).");

        // Idempotencia: si ya existe preferencia y URL, reutilizar sin llamar a MP
        if (!string.IsNullOrEmpty(turno.MpPreferenceId) && !string.IsNullOrEmpty(turno.MpCheckoutUrl))
            return turno.MpCheckoutUrl;

        var accessToken = await _oauthService.ObtenerAccessTokenVigenteAsync(turno.SucursalId);

        var monto  = turno.MontoSena > 0 ? turno.MontoSena!.Value : turno.PrecioServicio;
        var titulo = $"Seña — {turno.Servicio?.Nombre ?? "Servicio"}";

        var notificationUrl = $"{_backendUrl.TrimEnd('/')}/api/webhooks/mercadopago/{turno.SucursalId}";

        var (preferenceId, checkoutUrl) = await _mpGateway.CrearPreferenciaAsync(
            turnoId.ToString(),
            titulo,
            monto,
            _frontendUrl,
            accessToken,
            notificationUrl);

        turno.MpPreferenceId = preferenceId;
        turno.MpCheckoutUrl  = checkoutUrl;
        _turnoRepo.Update(turno);
        await _turnoRepo.SaveChangesAsync();

        return checkoutUrl;
    }

    /// <inheritdoc/>
    public async Task ConfirmarPagoDesdeRedirectAsync(long paymentId, Guid turnoId)
    {
        var turno = await _turnoRepo.GetByIdConRelacionesAsync(turnoId)
            ?? throw new InvalidOperationException($"Turno {turnoId} no encontrado.");

        await ProcesarNotificacionPagoAsync(paymentId, turno.SucursalId);
    }

    /// <inheritdoc/>
    public async Task<bool> VerificarPagoAsync(Guid turnoId)
    {
        var turno = await _turnoRepo.GetByIdConRelacionesAsync(turnoId)
            ?? throw new InvalidOperationException($"Turno {turnoId} no encontrado.");

        if (turno.Estado == EstadoTurno.Confirmado)
        {
            _logger.LogInformation("Turno {TurnoId} ya está Confirmado. VerificarPago ignorado.", turnoId);
            return true;
        }

        var accessToken = await _oauthService.ObtenerAccessTokenVigenteAsync(turno.SucursalId);
        var pago = await _mpGateway.BuscarPagoPorReferenciaAsync(turnoId.ToString(), accessToken);

        if (pago is null)
        {
            _logger.LogInformation("No se encontró pago aprobado para turno {TurnoId}.", turnoId);
            return false;
        }

        await ProcesarNotificacionPagoAsync(pago.PaymentId, turno.SucursalId);
        return true;
    }

    /// <inheritdoc/>
    public async Task ProcesarNotificacionPagoAsync(long paymentId, Guid sucursalId)
    {
        PaymentDetailsDto pago;
        try
        {
            var accessToken = await _oauthService.ObtenerAccessTokenVigenteAsync(sucursalId);
            pago = await _mpGateway.ObtenerDetallesPagoAsync(paymentId, accessToken);
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

        // Seguridad: validar que el turno pertenece a la sucursal del webhook
        if (turno.SucursalId != sucursalId)
        {
            _logger.LogWarning(
                "Mismatch de sucursal para PaymentId={PaymentId}. " +
                "Webhook sucursalId={WebhookSucursalId}, turno sucursalId={TurnoSucursalId}",
                paymentId, sucursalId, turno.SucursalId);
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

        // Seguridad: validar que el monto cobrado coincide con el esperado
        var montoEsperado = turno.MontoSena > 0 ? turno.MontoSena!.Value : turno.PrecioServicio;
        var esperado = decimal.Round(montoEsperado, 2);
        var recibido = decimal.Round(pago.Amount, 2);
        if (esperado != recibido)
        {
            _logger.LogWarning(
                "Monto inesperado para turno {TurnoId}. Esperado={Esperado}, Recibido={Recibido}. Pago {PaymentId} ignorado.",
                turnoId, esperado, recibido, paymentId);
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

        // Notificación en tiempo real: el turno ahora está confirmado
        if (_notificaciones is not null)
        {
            try
            {
                var turnoDto = new TurnoResponseDto
                {
                    Id               = turno.Id,
                    SucursalId       = turno.SucursalId,
                    ClienteId        = turno.ClienteId,
                    ClienteNombre    = turno.Cliente is not null
                                       ? $"{turno.Cliente.Nombre} {turno.Cliente.Apellido}".Trim()
                                       : string.Empty,
                    ClienteTelefono  = turno.Cliente?.Telefono ?? string.Empty,
                    ServicioId       = turno.ServicioId,
                    ServicioNombre   = turno.Servicio?.Nombre ?? string.Empty,
                    ProfesionalId    = turno.ProfesionalId,
                    ProfesionalNombre = turno.Profesional is not null
                                       ? $"{turno.Profesional.Nombre} {turno.Profesional.Apellido}".Trim()
                                       : string.Empty,
                    FechaHoraInicio  = turno.FechaHoraInicio,
                    FechaHoraFin     = turno.FechaHoraFin,
                    Estado           = turno.Estado,
                    Origen           = turno.Origen,
                    Observaciones    = turno.Observaciones,
                    TokenGestion     = turno.TokenGestion,
                    PrecioServicio   = turno.PrecioServicio,
                    MontoSena        = turno.MontoSena,
                    TotalCobrado     = turno.TotalCobrado,
                };
                await _notificaciones.NotificarNuevoTurnoAsync(turnoDto);
            }
            catch { /* SignalR es no-crítico */ }
        }

        _logger.LogInformation(
            "Turno {TurnoId} confirmado via Mercado Pago (PaymentId: {PaymentId}).", turnoId, paymentId);

        // Enviar emails de confirmación (cliente y staff) una vez que el pago fue aprobado
        var sucursal         = await _sucursalRepo.GetByIdAsync(turno.SucursalId);
        var sucursalNombre   = sucursal?.Nombre ?? "TurneroApp";
        var clienteNombre    = turno.Cliente is not null
            ? $"{turno.Cliente.Nombre} {turno.Cliente.Apellido}".Trim()
            : string.Empty;
        var servicioNombre   = turno.Servicio?.Nombre ?? string.Empty;
        var profesionalNombre = turno.Profesional is not null
            ? $"{turno.Profesional.Nombre} {turno.Profesional.Apellido}".Trim()
            : string.Empty;

        // Email al cliente
        var clienteEmail = turno.Cliente?.Email;
        if (!string.IsNullOrWhiteSpace(clienteEmail))
        {
            try
            {
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
                _logger.LogWarning(ex, "No se pudo enviar el email de confirmación al cliente para el turno {TurnoId}.", turnoId);
            }
        }

        // Email al profesional/staff (se envía aquí porque el turno venía de PendientePago)
        var profesionalEmail = turno.Profesional?.Email;
        if (!string.IsNullOrWhiteSpace(profesionalEmail))
        {
            try
            {
                await _emailService.EnviarNuevoTurnoStaffAsync(
                    profesionalEmail,
                    profesionalNombre,
                    sucursalNombre,
                    servicioNombre,
                    clienteNombre,
                    turno.FechaHoraInicio,
                    turno.FechaHoraFin);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo enviar el email de nuevo turno al staff para el turno {TurnoId}.", turnoId);
            }
        }
    }
}
