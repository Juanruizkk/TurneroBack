using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using TurneroApp.Application.DTOs.Pagos;
using TurneroApp.Application.Interfaces;
using TurneroApp.Application.Services;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;
using TurneroApp.Tests.Helpers;

namespace TurneroApp.Tests.Services;

/// <summary>
/// Suite de tests unitarios para <see cref="PagoTurnoService"/>.
/// Cubre los flujos: GenerarLinkDePago, pago aprobado, pago rechazado/cancelado,
/// checkout abandonado (pestaña cerrada) y casos borde de idempotencia y errores.
/// </summary>
public class PagoTurnoServiceTests
{
    // ── Mocks ─────────────────────────────────────────────────────────────────

    private readonly Mock<ITurnoRepository>          _turnoRepo    = new();
    private readonly Mock<ICajaRepository>           _cajaRepo     = new();
    private readonly Mock<ISucursalRepository>       _sucursalRepo = new();
    private readonly Mock<IMercadoPagoGateway>       _mpGateway    = new();
    private readonly Mock<IMercadoPagoOAuthService>  _oauthService = new();
    private readonly Mock<IEmailService>             _emailService = new();
    private readonly Mock<ILogger<PagoTurnoService>> _logger       = new();

    private readonly PagoTurnoService _sut;

    public PagoTurnoServiceTests()
    {
        _sut = new PagoTurnoService(
            _turnoRepo.Object,
            _cajaRepo.Object,
            _sucursalRepo.Object,
            _mpGateway.Object,
            _oauthService.Object,
            _emailService.Object,
            _logger.Object,
            "http://localhost:5173",
            "http://localhost:5000");

        // Defaults seguros para no necesitar configurar en cada test
        _turnoRepo.Setup(r => r.Update(It.IsAny<Turno>()));
        _turnoRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);
        _oauthService.Setup(o => o.ObtenerAccessTokenVigenteAsync(It.IsAny<Guid>()))
            .ReturnsAsync("test-access-token");
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
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static PaymentDetailsDto CrearPago(
        string status,
        Guid turnoId,
        decimal amount = 1500m)
        => new()
        {
            PaymentId         = 123456789L,
            Status            = status,
            StatusDetail      = status == "approved" ? "accredited" : status,
            ExternalReference = turnoId.ToString(),
            Amount            = amount,
        };

    private Turno CrearTurnoPendientePago(decimal? montoSena = 1500m)
    {
        var turno = TurnoTestFactory.CrearTurnoConRelaciones();
        turno.Estado     = EstadoTurno.PendientePago;
        turno.MontoSena  = montoSena;
        return turno;
    }

    // =========================================================================
    // ── GenerarLinkDePagoAsync ────────────────────────────────────────────────
    // =========================================================================

    [Fact]
    public async Task GenerarLink_TurnoNoEncontrado_LanzaException()
    {
        _turnoRepo.Setup(r => r.GetByIdConRelacionesAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Turno?)null);

        await _sut.Invoking(s => s.GenerarLinkDePagoAsync(Guid.NewGuid()))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no encontrado*");
    }

    [Fact]
    public async Task GenerarLink_TurnoNoEsPendientePago_LanzaException()
    {
        var turno = TurnoTestFactory.CrearTurnoConRelaciones();
        turno.Estado = EstadoTurno.Confirmado;

        _turnoRepo.Setup(r => r.GetByIdConRelacionesAsync(turno.Id))
            .ReturnsAsync(turno);

        await _sut.Invoking(s => s.GenerarLinkDePagoAsync(turno.Id))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*PendientePago*");
    }

    [Fact]
    public async Task GenerarLink_YaTienePreferenceId_RetornaExistenteSinLlamarAlGateway()
    {
        var turno = CrearTurnoPendientePago();
        turno.MpPreferenceId = "pref-existente-123";
        turno.MpCheckoutUrl  = "https://checkout.existing.com";

        _turnoRepo.Setup(r => r.GetByIdConRelacionesAsync(turno.Id))
            .ReturnsAsync(turno);

        var result = await _sut.GenerarLinkDePagoAsync(turno.Id);

        result.Should().Be("https://checkout.existing.com",
            "debe reutilizar el checkout URL ya generado (idempotencia)");
        _mpGateway.Verify(g => g.CrearPreferenciaAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()),
            Times.Never,
            "no debe crear una nueva preferencia si ya existe una");
    }

    [Fact]
    public async Task GenerarLink_SinPreferenceId_LlamaAlGatewayYPersiste()
    {
        var turno = CrearTurnoPendientePago();
        _turnoRepo.Setup(r => r.GetByIdConRelacionesAsync(turno.Id)).ReturnsAsync(turno);
        _mpGateway.Setup(g => g.CrearPreferenciaAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(("nueva-pref-456", "https://checkout.test/nueva-pref-456"));

        var result = await _sut.GenerarLinkDePagoAsync(turno.Id);

        result.Should().Be("https://checkout.test/nueva-pref-456");
        turno.MpPreferenceId.Should().Be("nueva-pref-456",
            "debe guardar el preferenceId en el turno");
        _turnoRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task GenerarLink_UsaMontoSena_CuandoMontoSenaEsMayorACero()
    {
        var turno = CrearTurnoPendientePago(montoSena: 2000m);
        turno.PrecioServicio = 8000m;
        _turnoRepo.Setup(r => r.GetByIdConRelacionesAsync(turno.Id)).ReturnsAsync(turno);
        _mpGateway.Setup(g => g.CrearPreferenciaAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(("pref-monto", "https://checkout.test/monto"));

        await _sut.GenerarLinkDePagoAsync(turno.Id);

        _mpGateway.Verify(g => g.CrearPreferenciaAsync(
            It.IsAny<string>(), It.IsAny<string>(), 2000m,
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()),
            Times.Once,
            "el monto pasado al gateway debe ser el de la seña, no el precio completo");
    }

    [Fact]
    public async Task GenerarLink_UsaPrecioServicio_CuandoMontoSenaEsNulo()
    {
        var turno = CrearTurnoPendientePago(montoSena: null);
        turno.PrecioServicio = 5000m;
        _turnoRepo.Setup(r => r.GetByIdConRelacionesAsync(turno.Id)).ReturnsAsync(turno);
        _mpGateway.Setup(g => g.CrearPreferenciaAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(("pref-precio", "https://checkout.test/precio"));

        await _sut.GenerarLinkDePagoAsync(turno.Id);

        _mpGateway.Verify(g => g.CrearPreferenciaAsync(
            It.IsAny<string>(), It.IsAny<string>(), 5000m,
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()),
            Times.Once,
            "sin seña configurada, se cobra el precio completo del servicio");
    }

    // =========================================================================
    // ── ProcesarNotificacionPagoAsync — casos de control ─────────────────────
    // =========================================================================

    [Fact]
    public async Task Procesar_ExternalReferenceInvalido_NoModificaNada()
    {
        var sucursalId = Guid.NewGuid();
        var pago = new PaymentDetailsDto
        {
            PaymentId = 1L, Status = "approved",
            ExternalReference = "esto-no-es-un-guid", Amount = 100m,
        };
        _mpGateway.Setup(g => g.ObtenerDetallesPagoAsync(1L, It.IsAny<string>())).ReturnsAsync(pago);

        await _sut.ProcesarNotificacionPagoAsync(1L, sucursalId);

        _turnoRepo.Verify(r => r.GetByIdConRelacionesAsync(It.IsAny<Guid>()), Times.Never,
            "si el ExternalReference no es un Guid válido, no debe buscar ningún turno");
    }

    [Fact]
    public async Task Procesar_TurnoNoEncontrado_NoLanzaExceptionNiModificaNada()
    {
        var turnoId    = Guid.NewGuid();
        var sucursalId = Guid.NewGuid();
        _mpGateway.Setup(g => g.ObtenerDetallesPagoAsync(It.IsAny<long>(), It.IsAny<string>()))
            .ReturnsAsync(CrearPago("approved", turnoId));
        _turnoRepo.Setup(r => r.GetByIdConRelacionesAsync(turnoId))
            .ReturnsAsync((Turno?)null);

        var act = () => _sut.ProcesarNotificacionPagoAsync(1L, sucursalId);

        await act.Should().NotThrowAsync();
        _turnoRepo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task Procesar_StatusPending_NoModificaTurno()
    {
        var turno = CrearTurnoPendientePago();
        _mpGateway.Setup(g => g.ObtenerDetallesPagoAsync(It.IsAny<long>(), It.IsAny<string>()))
            .ReturnsAsync(CrearPago("pending", turno.Id));
        _turnoRepo.Setup(r => r.GetByIdConRelacionesAsync(turno.Id)).ReturnsAsync(turno);

        await _sut.ProcesarNotificacionPagoAsync(1L, turno.SucursalId);

        turno.Estado.Should().Be(EstadoTurno.PendientePago,
            "un pago en estado 'pending' no debe cambiar el estado del turno");
        _turnoRepo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    // =========================================================================
    // ── ProcesarNotificacionPagoAsync — pago RECHAZADO / CANCELADO ────────────
    // =========================================================================

    [Theory]
    [InlineData("rejected")]
    [InlineData("cancelled")]
    public async Task Procesar_PagoRechazadoOCancelado_MarcaTurnoComoPagoRechazado(string status)
    {
        var turno = CrearTurnoPendientePago();
        _mpGateway.Setup(g => g.ObtenerDetallesPagoAsync(It.IsAny<long>(), It.IsAny<string>()))
            .ReturnsAsync(CrearPago(status, turno.Id));
        _turnoRepo.Setup(r => r.GetByIdConRelacionesAsync(turno.Id)).ReturnsAsync(turno);

        await _sut.ProcesarNotificacionPagoAsync(1L, turno.SucursalId);

        turno.Estado.Should().Be(EstadoTurno.PagoRechazado,
            $"un pago con status '{status}' debe marcar el turno como PagoRechazado para liberar el slot");
        _turnoRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Procesar_PagoRechazado_TurnoYaEnOtroEstado_NoModifica()
    {
        var turno = TurnoTestFactory.CrearTurnoConRelaciones();
        turno.Estado = EstadoTurno.CanceladoCliente;
        _mpGateway.Setup(g => g.ObtenerDetallesPagoAsync(It.IsAny<long>(), It.IsAny<string>()))
            .ReturnsAsync(CrearPago("rejected", turno.Id));
        _turnoRepo.Setup(r => r.GetByIdConRelacionesAsync(turno.Id)).ReturnsAsync(turno);

        await _sut.ProcesarNotificacionPagoAsync(1L, turno.SucursalId);

        turno.Estado.Should().Be(EstadoTurno.CanceladoCliente,
            "no debe sobrescribir el estado si el turno ya no está en PendientePago");
        _turnoRepo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    // =========================================================================
    // ── ProcesarNotificacionPagoAsync — pago APROBADO ─────────────────────────
    // =========================================================================

    [Fact]
    public async Task Procesar_PagoAprobado_TurnoYaConfirmado_Idempotencia()
    {
        var turno = TurnoTestFactory.CrearTurnoConRelaciones();
        turno.Estado = EstadoTurno.Confirmado;
        _mpGateway.Setup(g => g.ObtenerDetallesPagoAsync(It.IsAny<long>(), It.IsAny<string>()))
            .ReturnsAsync(CrearPago("approved", turno.Id));
        _turnoRepo.Setup(r => r.GetByIdConRelacionesAsync(turno.Id)).ReturnsAsync(turno);

        await _sut.ProcesarNotificacionPagoAsync(1L, turno.SucursalId);

        _turnoRepo.Verify(r => r.SaveChangesAsync(), Times.Never,
            "un webhook duplicado no debe re-procesar un turno ya confirmado");
        _cajaRepo.Verify(r => r.RegistrarMovimientoTransaccionalAsync(
            It.IsAny<MovimientoCaja>(), It.IsAny<PagoTurno>(), It.IsAny<Turno>()),
            Times.Never);
    }

    [Fact]
    public async Task Procesar_PagoAprobado_ConfirmaTurnoYActualizaTotalCobrado()
    {
        var turno = CrearTurnoPendientePago();
        var caja  = new Caja { Id = Guid.NewGuid(), SucursalId = turno.SucursalId };
        _mpGateway.Setup(g => g.ObtenerDetallesPagoAsync(It.IsAny<long>(), It.IsAny<string>()))
            .ReturnsAsync(CrearPago("approved", turno.Id, amount: 1500m));
        _turnoRepo.Setup(r => r.GetByIdConRelacionesAsync(turno.Id)).ReturnsAsync(turno);
        _cajaRepo.Setup(r => r.GetBySucursalAsync(turno.SucursalId)).ReturnsAsync(caja);

        await _sut.ProcesarNotificacionPagoAsync(1L, turno.SucursalId);

        turno.Estado.Should().Be(EstadoTurno.Confirmado,
            "un pago aprobado debe confirmar el turno");
        turno.TotalCobrado.Should().Be(1500m,
            "el monto cobrado debe actualizarse con el monto del pago");
    }

    [Fact]
    public async Task Procesar_PagoAprobado_RegistraMovimientoEnCaja()
    {
        var turno = CrearTurnoPendientePago();
        var caja  = new Caja { Id = Guid.NewGuid(), SucursalId = turno.SucursalId };
        _mpGateway.Setup(g => g.ObtenerDetallesPagoAsync(It.IsAny<long>(), It.IsAny<string>()))
            .ReturnsAsync(CrearPago("approved", turno.Id));
        _turnoRepo.Setup(r => r.GetByIdConRelacionesAsync(turno.Id)).ReturnsAsync(turno);
        _cajaRepo.Setup(r => r.GetBySucursalAsync(turno.SucursalId)).ReturnsAsync(caja);

        await _sut.ProcesarNotificacionPagoAsync(1L, turno.SucursalId);

        _cajaRepo.Verify(r => r.RegistrarMovimientoTransaccionalAsync(
            It.Is<MovimientoCaja>(m =>
                m.CajaId == caja.Id &&
                m.TurnoId == turno.Id &&
                m.UsuarioId == null &&
                m.MedioPago == MedioPago.MercadoPago),
            It.IsAny<PagoTurno>(),
            It.IsAny<Turno>()),
            Times.Once,
            "debe registrar un movimiento de Caja con MedioPago=MercadoPago y UsuarioId=null");
    }

    [Fact]
    public async Task Procesar_PagoAprobado_SinCaja_ConfirmaTurnoSinMovimiento()
    {
        var turno = CrearTurnoPendientePago();
        _mpGateway.Setup(g => g.ObtenerDetallesPagoAsync(It.IsAny<long>(), It.IsAny<string>()))
            .ReturnsAsync(CrearPago("approved", turno.Id));
        _turnoRepo.Setup(r => r.GetByIdConRelacionesAsync(turno.Id)).ReturnsAsync(turno);
        _cajaRepo.Setup(r => r.GetBySucursalAsync(turno.SucursalId))
            .ReturnsAsync((Caja?)null);

        await _sut.ProcesarNotificacionPagoAsync(1L, turno.SucursalId);

        turno.Estado.Should().Be(EstadoTurno.Confirmado,
            "el turno debe confirmarse aunque no haya Caja (no puede quedar atascado en PendientePago)");
        _cajaRepo.Verify(r => r.RegistrarMovimientoTransaccionalAsync(
            It.IsAny<MovimientoCaja>(), It.IsAny<PagoTurno>(), It.IsAny<Turno>()),
            Times.Never);
        _turnoRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Procesar_PagoAprobado_ClienteConEmail_EnviaEmailConfirmacion()
    {
        var cliente = TurnoTestFactory.CrearCliente("cliente@test.com");
        var turno   = TurnoTestFactory.CrearTurnoConRelaciones(cliente: cliente);
        turno.Estado    = EstadoTurno.PendientePago;
        turno.MontoSena = 1500m;
        var caja = new Caja { Id = Guid.NewGuid(), SucursalId = turno.SucursalId };
        _mpGateway.Setup(g => g.ObtenerDetallesPagoAsync(It.IsAny<long>(), It.IsAny<string>()))
            .ReturnsAsync(CrearPago("approved", turno.Id));
        _turnoRepo.Setup(r => r.GetByIdConRelacionesAsync(turno.Id)).ReturnsAsync(turno);
        _cajaRepo.Setup(r => r.GetBySucursalAsync(turno.SucursalId)).ReturnsAsync(caja);

        await _sut.ProcesarNotificacionPagoAsync(1L, turno.SucursalId);

        _emailService.Verify(e => e.EnviarConfirmacionTurnoAsync(
            "cliente@test.com",
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>()),
            Times.Once,
            "debe enviar email de confirmación al cliente cuando el pago es aprobado");
    }

    [Fact]
    public async Task Procesar_PagoAprobado_ClienteSinEmail_NoEnviaEmail()
    {
        var cliente = TurnoTestFactory.CrearCliente(email: null);
        var turno   = TurnoTestFactory.CrearTurnoConRelaciones(cliente: cliente);
        turno.Estado    = EstadoTurno.PendientePago;
        turno.MontoSena = 1500m;
        var caja = new Caja { Id = Guid.NewGuid(), SucursalId = turno.SucursalId };
        _mpGateway.Setup(g => g.ObtenerDetallesPagoAsync(It.IsAny<long>(), It.IsAny<string>()))
            .ReturnsAsync(CrearPago("approved", turno.Id));
        _turnoRepo.Setup(r => r.GetByIdConRelacionesAsync(turno.Id)).ReturnsAsync(turno);
        _cajaRepo.Setup(r => r.GetBySucursalAsync(turno.SucursalId)).ReturnsAsync(caja);

        await _sut.ProcesarNotificacionPagoAsync(1L, turno.SucursalId);

        _emailService.Verify(e => e.EnviarConfirmacionTurnoAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Procesar_PagoAprobado_EmailFalla_NoPropagarExcepcion()
    {
        var cliente = TurnoTestFactory.CrearCliente("cliente@test.com");
        var turno   = TurnoTestFactory.CrearTurnoConRelaciones(cliente: cliente);
        turno.Estado    = EstadoTurno.PendientePago;
        turno.MontoSena = 1500m;
        var caja = new Caja { Id = Guid.NewGuid(), SucursalId = turno.SucursalId };
        _mpGateway.Setup(g => g.ObtenerDetallesPagoAsync(It.IsAny<long>(), It.IsAny<string>()))
            .ReturnsAsync(CrearPago("approved", turno.Id));
        _turnoRepo.Setup(r => r.GetByIdConRelacionesAsync(turno.Id)).ReturnsAsync(turno);
        _cajaRepo.Setup(r => r.GetBySucursalAsync(turno.SucursalId)).ReturnsAsync(caja);
        _emailService.Setup(e => e.EnviarConfirmacionTurnoAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("SMTP timeout"));

        var act = () => _sut.ProcesarNotificacionPagoAsync(1L, turno.SucursalId);

        await act.Should().NotThrowAsync(
            "el fallo del email no debe revertir la confirmación del turno ni propagar la excepción");
        turno.Estado.Should().Be(EstadoTurno.Confirmado,
            "el turno debe quedar confirmado aunque el email haya fallado");
    }
}
