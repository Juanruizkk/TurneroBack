using Microsoft.Extensions.Logging;
using TurneroApp.Application.DTOs.Pagos;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.Services;

public sealed class PagoSuscripcionService : IPagoSuscripcionService
{
    private readonly ISuscripcionRepository          _suscripcionRepo;
    private readonly IMercadoPagoGateway             _mpGateway;
    private readonly ILogger<PagoSuscripcionService> _logger;
    private readonly string                          _frontendUrl;
    private readonly string                          _backendUrl;
    private readonly string                          _masterAccessToken;

    public PagoSuscripcionService(
        ISuscripcionRepository          suscripcionRepo,
        IMercadoPagoGateway             mpGateway,
        ILogger<PagoSuscripcionService> logger,
        string                          frontendUrl,
        string                          backendUrl,
        string                          masterAccessToken)
    {
        _suscripcionRepo   = suscripcionRepo;
        _mpGateway         = mpGateway;
        _logger            = logger;
        _frontendUrl       = frontendUrl.TrimEnd('/');
        _backendUrl        = backendUrl.TrimEnd('/');
        _masterAccessToken = masterAccessToken;
    }

    /// <inheritdoc/>
    public async Task<string> GenerarLinkDePagoAsync(Guid sucursalId, Guid planId)
    {
        var plan = await _suscripcionRepo.GetPlanByIdAsync(planId)
            ?? throw new InvalidOperationException($"Plan {planId} no encontrado.");

        if (string.IsNullOrEmpty(_masterAccessToken))
            throw new InvalidOperationException("MasterAccessToken de MercadoPago no configurado.");

        var externalReference = $"suscripcion:{sucursalId}:{planId}";
        var notificationUrl   = $"{_backendUrl}/api/webhooks/mercadopago-suscripcion";
        var successUrl        = $"{_frontendUrl}/admin/suscripcion?pago=exitoso";
        var failureUrl        = $"{_frontendUrl}/admin/suscripcion?pago=fallido";

        var (_, checkoutUrl) = await _mpGateway.CrearPreferenciaAsync(
            externalReference,
            $"Plan {plan.Nombre} — suscripción mensual",
            plan.PrecioMensual,
            _frontendUrl,
            _masterAccessToken,
            notificationUrl,
            successUrl,
            failureUrl);

        _logger.LogInformation(
            "Preferencia de suscripción creada para sucursal {SucursalId}, plan {PlanNombre}.",
            sucursalId, plan.Nombre);

        return checkoutUrl;
    }

    /// <inheritdoc/>
    public async Task ProcesarWebhookAsync(long paymentId)
    {
        if (string.IsNullOrEmpty(_masterAccessToken))
        {
            _logger.LogWarning("MasterAccessToken no configurado, no se puede procesar webhook de suscripción.");
            return;
        }

        PaymentDetailsDto pago;
        try
        {
            pago = await _mpGateway.ObtenerDetallesPagoAsync(paymentId, _masterAccessToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo detalles del pago {PaymentId} para suscripción.", paymentId);
            return;
        }

        if (pago.Status != "approved")
        {
            _logger.LogInformation(
                "Pago {PaymentId} ignorado (status={Status}).", paymentId, pago.Status);
            return;
        }

        // Formato esperado: "suscripcion:{sucursalId}:{planId}"
        var parts = pago.ExternalReference.Split(':');
        if (parts.Length != 3 || parts[0] != "suscripcion")
        {
            _logger.LogInformation(
                "Pago {PaymentId} ignorado (ExternalReference={Ref}, no es suscripción).",
                paymentId, pago.ExternalReference);
            return;
        }

        if (!Guid.TryParse(parts[1], out var sucursalId) || !Guid.TryParse(parts[2], out var planId))
        {
            _logger.LogWarning(
                "ExternalReference inválido en pago {PaymentId}: '{Ref}'.", paymentId, pago.ExternalReference);
            return;
        }

        var plan = await _suscripcionRepo.GetPlanByIdAsync(planId);
        if (plan is null)
        {
            _logger.LogWarning("Plan {PlanId} no encontrado al procesar webhook de suscripción.", planId);
            return;
        }

        var suscripcion = await _suscripcionRepo.GetBySucursalAsync(sucursalId);
        if (suscripcion is null)
        {
            _logger.LogWarning(
                "No existe SucursalSuscripcion para sucursal {SucursalId}.", sucursalId);
            return;
        }

        // Evitar duplicados si MP reintenta el webhook
        if (await _suscripcionRepo.ExistePagoAsync(paymentId))
        {
            _logger.LogInformation("Pago {PaymentId} ya registrado, se ignora duplicado.", paymentId);
            return;
        }

        // Entidad cargada con tracking → las asignaciones son detectadas automáticamente por EF.
        // NO llamar a Update() para no marcar el Plan del grafo como Modified.
        suscripcion.PlanId           = planId;
        suscripcion.FechaVencimiento = DateTime.UtcNow.AddDays(30);
        suscripcion.Estado           = EstadoSuscripcion.Activa;
        suscripcion.EnPeriodoPrueba  = false;

        await _suscripcionRepo.AddPagoAsync(new PagoSuscripcion
        {
            Id          = Guid.NewGuid(),
            SucursalId  = sucursalId,
            PlanId      = planId,
            PlanNombre  = plan.Nombre,
            Monto       = pago.Amount,
            MPPaymentId = paymentId,
            FechaPago   = DateTime.UtcNow,
        });

        await _suscripcionRepo.SaveChangesAsync();

        _logger.LogInformation(
            "Suscripción de sucursal {SucursalId} actualizada a plan {PlanNombre}.",
            sucursalId, plan.Nombre);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<PagoSuscripcionDto>> GetHistorialAsync(Guid sucursalId)
    {
        var pagos = await _suscripcionRepo.GetHistorialBySucursalAsync(sucursalId);

        return pagos.Select(p => new PagoSuscripcionDto
        {
            Id          = p.Id,
            PlanNombre  = p.PlanNombre,
            Monto       = p.Monto,
            MPPaymentId = p.MPPaymentId,
            FechaPago   = p.FechaPago,
        });
    }
}
