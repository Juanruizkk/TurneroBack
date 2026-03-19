using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using TurneroApp.Application.DTOs.Pagos;
using TurneroApp.Application.DTOs.Turno;
using TurneroApp.Application.Interfaces;
using TurneroApp.Application.Services;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;
using TurneroApp.Tests.Helpers;

namespace TurneroApp.Tests.Services;

/// <summary>
/// Verifica el comportamiento de las notificaciones SignalR en <see cref="PagoTurnoService"/>.
///
/// Reglas de negocio bajo prueba:
/// • Cuando MercadoPago aprueba un pago ("approved"), se llama a
///   <c>NotificarNuevoTurnoAsync</c> con un DTO cuyo estado es <c>Confirmado</c>.
///   Esto permite que el calendario del admin se actualice en tiempo real.
/// • Para pagos rechazados o cancelados, no se dispara ninguna notificación.
/// • Si SignalR falla, el fallo es no-crítico: el turno ya quedó confirmado y el
///   movimiento de caja ya fue registrado; la excepción no debe propagarse.
/// </summary>
public class PagoTurnoServiceSignalRTests
{
    // ── Mocks ─────────────────────────────────────────────────────────────────

    private readonly Mock<ITurnoRepository>          _turnoRepo      = new();
    private readonly Mock<ICajaRepository>           _cajaRepo       = new();
    private readonly Mock<ISucursalRepository>       _sucursalRepo   = new();
    private readonly Mock<IMercadoPagoGateway>       _mpGateway      = new();
    private readonly Mock<IEmailService>             _emailService   = new();
    private readonly Mock<ILogger<PagoTurnoService>> _logger         = new();
    private readonly Mock<ITurnoNotificacionService> _notificaciones = new();
    private readonly PagoTurnoService                _sut;

    public PagoTurnoServiceSignalRTests()
    {
        _sut = new PagoTurnoService(
            _turnoRepo.Object,
            _cajaRepo.Object,
            _sucursalRepo.Object,
            _mpGateway.Object,
            _emailService.Object,
            _logger.Object,
            "http://localhost:5173",
            _notificaciones.Object);

        // Defaults seguros para no configurar en cada test
        _turnoRepo.Setup(r => r.Update(It.IsAny<Turno>()));
        _turnoRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);
        _emailService.Setup(e => e.EnviarConfirmacionTurnoAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _cajaRepo.Setup(r => r.RegistrarMovimientoTransaccionalAsync(
                It.IsAny<MovimientoCaja>(), It.IsAny<PagoTurno>(), It.IsAny<Turno>()))
            .ReturnsAsync((MovimientoCaja m, PagoTurno _, Turno _) => m);
        _sucursalRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(TurnoTestFactory.CrearSucursal());
        _notificaciones
            .Setup(n => n.NotificarNuevoTurnoAsync(It.IsAny<TurnoResponseDto>()))
            .Returns(Task.CompletedTask);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static PaymentDetailsDto CrearPago(string status, Guid turnoId, decimal amount = 1500m)
        => new()
        {
            PaymentId         = 999888777L,
            Status            = status,
            StatusDetail      = status == "approved" ? "accredited" : status,
            ExternalReference = turnoId.ToString(),
            Amount            = amount,
        };

    private Turno CrearTurnoPendientePago()
    {
        var turno    = TurnoTestFactory.CrearTurnoConRelaciones();
        turno.Estado = EstadoTurno.PendientePago;
        turno.MontoSena = 1500m;
        var caja = new Caja { Id = Guid.NewGuid(), SucursalId = turno.SucursalId };
        _cajaRepo.Setup(r => r.GetBySucursalAsync(turno.SucursalId)).ReturnsAsync(caja);
        return turno;
    }

    // =========================================================================
    // ── Pago APROBADO — debe disparar notificación ────────────────────────────
    // =========================================================================

    [Fact]
    public async Task Procesar_PagoAprobado_DisparaNotificacionNuevoTurno()
    {
        var turno = CrearTurnoPendientePago();
        _mpGateway.Setup(g => g.ObtenerDetallesPagoAsync(It.IsAny<long>()))
            .ReturnsAsync(CrearPago("approved", turno.Id));
        _turnoRepo.Setup(r => r.GetByIdConRelacionesAsync(turno.Id)).ReturnsAsync(turno);

        await _sut.ProcesarNotificacionPagoAsync(1L);

        _notificaciones.Verify(
            n => n.NotificarNuevoTurnoAsync(It.IsAny<TurnoResponseDto>()),
            Times.Once,
            "cuando el pago es aprobado, el turno recién confirmado debe notificarse " +
            "en tiempo real para que el calendario del admin se actualice sin recargar");
    }

    [Fact]
    public async Task Procesar_PagoAprobado_NotificacionPayload_EstadoEsConfirmado()
    {
        var turno = CrearTurnoPendientePago();
        _mpGateway.Setup(g => g.ObtenerDetallesPagoAsync(It.IsAny<long>()))
            .ReturnsAsync(CrearPago("approved", turno.Id, amount: 2000m));
        _turnoRepo.Setup(r => r.GetByIdConRelacionesAsync(turno.Id)).ReturnsAsync(turno);

        TurnoResponseDto? capturado = null;
        _notificaciones
            .Setup(n => n.NotificarNuevoTurnoAsync(It.IsAny<TurnoResponseDto>()))
            .Callback<TurnoResponseDto>(dto => capturado = dto)
            .Returns(Task.CompletedTask);

        await _sut.ProcesarNotificacionPagoAsync(1L);

        capturado.Should().NotBeNull();
        capturado!.Estado.Should().Be(EstadoTurno.Confirmado,
            "el payload debe reflejar el estado definitivo del turno: Confirmado");
        capturado.TotalCobrado.Should().Be(2000m,
            "el monto cobrado debe estar actualizado con el pago recibido de MercadoPago");
        capturado.SucursalId.Should().Be(turno.SucursalId,
            "la notificación debe incluir el SucursalId para que el hub enrute al grupo correcto");
        capturado.ProfesionalId.Should().Be(turno.ProfesionalId,
            "la notificación debe incluir el ProfesionalId para el grupo 'profesional-{id}'");
    }

    // =========================================================================
    // ── Pago RECHAZADO / CANCELADO — no debe notificar ────────────────────────
    // =========================================================================

    [Theory]
    [InlineData("rejected")]
    [InlineData("cancelled")]
    public async Task Procesar_PagoRechazadoOCancelado_NoDisparaNotificacion(string status)
    {
        var turno = CrearTurnoPendientePago();
        _mpGateway.Setup(g => g.ObtenerDetallesPagoAsync(It.IsAny<long>()))
            .ReturnsAsync(CrearPago(status, turno.Id));
        _turnoRepo.Setup(r => r.GetByIdConRelacionesAsync(turno.Id)).ReturnsAsync(turno);

        await _sut.ProcesarNotificacionPagoAsync(1L);

        _notificaciones.Verify(
            n => n.NotificarNuevoTurnoAsync(It.IsAny<TurnoResponseDto>()),
            Times.Never,
            $"un pago '{status}' no debe generar una notificación de nuevo turno " +
            "ya que el turno no pasó a estado Confirmado");
    }

    // =========================================================================
    // ── Resiliencia — fallo de SignalR no-crítico ─────────────────────────────
    // =========================================================================

    [Fact]
    public async Task Procesar_PagoAprobado_SignalRFalla_NoPropagarExcepcion()
    {
        var turno = CrearTurnoPendientePago();
        _mpGateway.Setup(g => g.ObtenerDetallesPagoAsync(It.IsAny<long>()))
            .ReturnsAsync(CrearPago("approved", turno.Id));
        _turnoRepo.Setup(r => r.GetByIdConRelacionesAsync(turno.Id)).ReturnsAsync(turno);
        _notificaciones
            .Setup(n => n.NotificarNuevoTurnoAsync(It.IsAny<TurnoResponseDto>()))
            .ThrowsAsync(new Exception("Hub desconectado"));

        var act = () => _sut.ProcesarNotificacionPagoAsync(1L);

        await act.Should().NotThrowAsync(
            "un fallo de SignalR no debe revertir la confirmación del turno ni " +
            "propagar la excepción — el turno ya quedó confirmado y la caja registrada");

        turno.Estado.Should().Be(EstadoTurno.Confirmado,
            "el turno debe quedar en Confirmado aunque la notificación haya fallado");
    }
}
