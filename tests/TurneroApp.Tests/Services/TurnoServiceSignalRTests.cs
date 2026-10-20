using FluentAssertions;
using Moq;
using Xunit;
using TurneroApp.Application.DTOs.Turno;
using TurneroApp.Application.Interfaces;
using TurneroApp.Application.Services;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;
using TurneroApp.Tests.Helpers;

namespace TurneroApp.Tests.Services;

/// <summary>
/// Verifica el comportamiento de las notificaciones SignalR en <see cref="TurnoService"/>.
///
/// Reglas de negocio bajo prueba:
/// • CreateAsync solo dispara <c>NotificarNuevoTurnoAsync</c> cuando el turno
///   queda en estado <c>Confirmado</c> de forma inmediata.
/// • Si el turno queda en <c>PendientePago</c> o <c>Pendiente</c>, no se notifica
///   (la notificación se dispara cuando el pago es aprobado por MercadoPago).
/// • UpdateEstadoAsync dispara <c>NotificarCancelacionAsync</c> solo para
///   <c>CanceladoStaff</c> y <c>CanceladoCliente</c>; para cualquier otro estado no notifica.
/// • Los fallos de SignalR son no-críticos: no deben interrumpir la operación.
/// </summary>
public class TurnoServiceSignalRTests
{
    // ── Mocks ─────────────────────────────────────────────────────────────────

    private readonly Mock<ITurnoRepository>           _turnoRepo      = new();
    private readonly Mock<IServicioRepository>        _servicioRepo   = new();
    private readonly Mock<IClienteRepository>         _clienteRepo    = new();
    private readonly Mock<ISucursalRepository>        _sucursalRepo   = new();
    private readonly Mock<IEmailService>              _emailService   = new();
    private readonly Mock<IComentarioTurnoRepository> _comentarioRepo = new();
    private readonly Mock<ITurnoNotificacionService>  _notificaciones = new();
    private readonly TurnoService                     _sut;

    public TurnoServiceSignalRTests()
    {
        _sut = new TurnoService(
            _turnoRepo.Object,
            _servicioRepo.Object,
            _clienteRepo.Object,
            _sucursalRepo.Object,
            _emailService.Object,
            _comentarioRepo.Object,
            "http://localhost:3000",
            _notificaciones.Object);

        // Default: todas las notificaciones completan sin error
        _notificaciones
            .Setup(n => n.NotificarNuevoTurnoAsync(It.IsAny<TurnoResponseDto>()))
            .Returns(Task.CompletedTask);
        _notificaciones
            .Setup(n => n.NotificarCancelacionAsync(It.IsAny<TurnoResponseDto>()))
            .Returns(Task.CompletedTask);
    }

    // ── Setup helpers ──────────────────────────────────────────────────────────

    private void SetupCrearOk(Servicio servicio, Cliente cliente, Sucursal sucursal)
    {
        _servicioRepo.Setup(r => r.GetByIdConDiasAsync(servicio.Id)).ReturnsAsync(servicio);
        _clienteRepo.Setup(r  => r.GetByIdAsync(cliente.Id)).ReturnsAsync(cliente);
        _sucursalRepo.Setup(r => r.GetByIdConConfiguracionAsync(It.IsAny<Guid>()))
            .ReturnsAsync(sucursal);
        _sucursalRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(sucursal);
        _turnoRepo.Setup(r => r.ExisteConflictoHorarioAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), null))
            .ReturnsAsync(false);
        _turnoRepo.Setup(r => r.AddAsync(It.IsAny<Turno>())).Returns(Task.CompletedTask);
        _turnoRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);
        _turnoRepo.Setup(r => r.GetByIdConRelacionesAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Turno?)null);
        _emailService.Setup(e => e.EnviarConfirmacionTurnoAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
    }

    private void SetupUpdateOk(Turno turno)
    {
        _turnoRepo.Setup(r => r.GetByIdConRelacionesAsync(turno.Id)).ReturnsAsync(turno);
        _turnoRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);
    }

    // =========================================================================
    // ── CreateAsync — notificación de nuevo turno ─────────────────────────────
    // =========================================================================

    [Fact]
    public async Task Create_EstadoConfirmado_DisparaNotificacionNuevoTurno()
    {
        // Origen Administrativo → siempre Confirmado
        var servicio = TurnoTestFactory.CrearServicio();
        var cliente  = TurnoTestFactory.CrearCliente();
        var sucursal = TurnoTestFactory.CrearSucursalConConfig();
        var dto      = TurnoTestFactory.CrearDto(
            servicioId: servicio.Id, clienteId: cliente.Id,
            origen: OrigenTurno.Administrativo);

        SetupCrearOk(servicio, cliente, sucursal);

        await _sut.CreateAsync(dto);

        _notificaciones.Verify(
            n => n.NotificarNuevoTurnoAsync(It.IsAny<TurnoResponseDto>()),
            Times.Once,
            "debe notificar en tiempo real cuando el turno queda confirmado de inmediato");
    }

    [Fact]
    public async Task Create_EstadoPendientePago_NoDisparaNotificacion()
    {
        // Un turno Web con seña → PendientePago → no notifica hasta que el pago se confirme
        var servicio = TurnoTestFactory.CrearServicio(
            requiereSena: true, tipoSena: TipoSena.Fija, valorSena: 1500m);
        var cliente  = TurnoTestFactory.CrearCliente();
        var sucursal = TurnoTestFactory.CrearSucursalConConfig();
        var dto      = TurnoTestFactory.CrearDto(
            servicioId: servicio.Id, clienteId: cliente.Id,
            origen: OrigenTurno.Web);

        SetupCrearOk(servicio, cliente, sucursal);

        await _sut.CreateAsync(dto);

        _notificaciones.Verify(
            n => n.NotificarNuevoTurnoAsync(It.IsAny<TurnoResponseDto>()),
            Times.Never,
            "no debe notificar si el turno queda en PendientePago — la notificación " +
            "se dispara solo cuando MercadoPago aprueba el pago");
    }

    [Fact]
    public async Task Create_EstadoPendiente_ModoManual_DisparaNotificacion()
    {
        // Turno Web + modo Manual → Pendiente → SÍ notifica al staff para que sepa que hay una solicitud pendiente
        var servicio = TurnoTestFactory.CrearServicio();
        var cliente  = TurnoTestFactory.CrearCliente();
        var sucursal = TurnoTestFactory.CrearSucursalConConfig(ModoConfirmacionTurnos.Manual);
        var dto      = TurnoTestFactory.CrearDto(
            servicioId: servicio.Id, clienteId: cliente.Id,
            origen: OrigenTurno.Web);

        SetupCrearOk(servicio, cliente, sucursal);

        await _sut.CreateAsync(dto);

        _notificaciones.Verify(
            n => n.NotificarNuevoTurnoAsync(It.IsAny<TurnoResponseDto>()),
            Times.Once,
            "debe notificar cuando el turno queda en Pendiente (modo Manual) para que el staff sepa que hay una solicitud");
    }

    [Fact]
    public async Task Create_SignalRFalla_NoLanzaException()
    {
        var servicio = TurnoTestFactory.CrearServicio();
        var cliente  = TurnoTestFactory.CrearCliente();
        var sucursal = TurnoTestFactory.CrearSucursalConConfig();
        var dto      = TurnoTestFactory.CrearDto(
            servicioId: servicio.Id, clienteId: cliente.Id,
            origen: OrigenTurno.Administrativo);

        SetupCrearOk(servicio, cliente, sucursal);

        _notificaciones
            .Setup(n => n.NotificarNuevoTurnoAsync(It.IsAny<TurnoResponseDto>()))
            .ThrowsAsync(new Exception("SignalR hub no disponible"));

        await _sut.Invoking(s => s.CreateAsync(dto))
            .Should().NotThrowAsync(
                "un fallo de SignalR no debe cancelar ni revertir la creación del turno");
    }

    [Fact]
    public async Task Create_NotificacionPayload_ContieneEstadoConfirmado()
    {
        var servicio = TurnoTestFactory.CrearServicio();
        var cliente  = TurnoTestFactory.CrearCliente();
        var sucursal = TurnoTestFactory.CrearSucursalConConfig();
        var dto      = TurnoTestFactory.CrearDto(
            servicioId: servicio.Id, clienteId: cliente.Id,
            origen: OrigenTurno.Administrativo);

        SetupCrearOk(servicio, cliente, sucursal);

        TurnoResponseDto? capturado = null;
        _notificaciones
            .Setup(n => n.NotificarNuevoTurnoAsync(It.IsAny<TurnoResponseDto>()))
            .Callback<TurnoResponseDto>(dto => capturado = dto)
            .Returns(Task.CompletedTask);

        await _sut.CreateAsync(dto);

        capturado.Should().NotBeNull();
        capturado!.Estado.Should().Be(EstadoTurno.Confirmado,
            "el payload de la notificación debe reflejar el estado real del turno");
        capturado.TotalCobrado.Should().Be(0m,
            "el turno recién creado no tiene cobros registrados");
    }

    // =========================================================================
    // ── UpdateEstadoAsync — notificación de cancelación ───────────────────────
    // =========================================================================

    [Theory]
    [InlineData(EstadoTurno.CanceladoStaff)]
    [InlineData(EstadoTurno.CanceladoCliente)]
    public async Task Update_Cancelacion_DisparaNotificacionCancelacion(EstadoTurno estado)
    {
        // Cliente sin email → omite la sección de email y simplifica el setup
        var cliente = TurnoTestFactory.CrearCliente(email: null);
        var turno   = TurnoTestFactory.CrearTurnoConRelaciones(cliente: cliente);
        var dto     = new UpdateTurnoDto { Estado = estado };

        SetupUpdateOk(turno);

        await _sut.UpdateEstadoAsync(turno.Id, dto);

        _notificaciones.Verify(
            n => n.NotificarCancelacionAsync(It.IsAny<TurnoResponseDto>()),
            Times.Once,
            $"debe notificar cancelación cuando el estado es {estado}");
    }

    [Theory]
    [InlineData(EstadoTurno.Completado)]
    [InlineData(EstadoTurno.NoShow)]
    [InlineData(EstadoTurno.Confirmado)]
    [InlineData(EstadoTurno.Reprogramado)]
    public async Task Update_EstadoNoCancelacion_NoDisparaNotificacion(EstadoTurno estado)
    {
        var turno = TurnoTestFactory.CrearTurnoConRelaciones();
        var dto   = new UpdateTurnoDto { Estado = estado };

        SetupUpdateOk(turno);

        await _sut.UpdateEstadoAsync(turno.Id, dto);

        _notificaciones.Verify(
            n => n.NotificarCancelacionAsync(It.IsAny<TurnoResponseDto>()),
            Times.Never,
            $"no debe notificar cancelación para el estado {estado}");
    }

    [Fact]
    public async Task Update_CancelacionSignalRFalla_NoLanzaException()
    {
        var cliente = TurnoTestFactory.CrearCliente(email: null);
        var turno   = TurnoTestFactory.CrearTurnoConRelaciones(cliente: cliente);
        var dto     = new UpdateTurnoDto { Estado = EstadoTurno.CanceladoStaff };

        SetupUpdateOk(turno);

        _notificaciones
            .Setup(n => n.NotificarCancelacionAsync(It.IsAny<TurnoResponseDto>()))
            .ThrowsAsync(new Exception("SignalR connection lost"));

        await _sut.Invoking(s => s.UpdateEstadoAsync(turno.Id, dto))
            .Should().NotThrowAsync(
                "un fallo de SignalR no debe impedir que la cancelación se persista correctamente");
    }
}
