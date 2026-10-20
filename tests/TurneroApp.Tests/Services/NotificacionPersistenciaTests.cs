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
/// Verifica que <see cref="TurnoService"/> persiste notificaciones en la base de datos
/// mediante <see cref="INotificacionRepository"/> para que sean visibles al reabrir el panel,
/// independientemente de si el usuario estaba conectado vía SignalR en ese momento.
///
/// Reglas bajo prueba:
/// • <c>CreateAsync</c> con estado <c>Confirmado</c>   → persiste una <c>Notificacion</c> de tipo <c>NuevoTurno</c>.
/// • <c>CreateAsync</c> con estado <c>PendientePago</c> → NO persiste (la notificación se genera al confirmar el pago).
/// • <c>CreateAsync</c> con estado <c>Pendiente</c> (modo Manual) → NO persiste.
/// • <c>CreateAsync</c> con observaciones → el mensaje incluye "Tiene un mensaje del cliente".
/// • <c>UpdateEstadoAsync</c> con <c>CanceladoStaff/CanceladoCliente</c> → persiste tipo <c>Cancelacion</c>.
/// • <c>UpdateEstadoAsync</c> con <c>Completado/NoShow</c>             → NO persiste.
/// • <c>CancelarPorTokenAsync</c>                                       → persiste tipo <c>Cancelacion</c>.
/// • Un fallo del repositorio de notificaciones nunca cancela la operación principal.
/// </summary>
public class NotificacionPersistenciaTests
{
    // ── Mocks ─────────────────────────────────────────────────────────────────

    private readonly Mock<ITurnoRepository>           _turnoRepo       = new();
    private readonly Mock<IServicioRepository>        _servicioRepo    = new();
    private readonly Mock<IClienteRepository>         _clienteRepo     = new();
    private readonly Mock<ISucursalRepository>        _sucursalRepo    = new();
    private readonly Mock<IEmailService>              _emailService    = new();
    private readonly Mock<IComentarioTurnoRepository> _comentarioRepo  = new();
    private readonly Mock<INotificacionRepository>    _notificacionRepo = new();
    private readonly TurnoService                     _sut;

    public NotificacionPersistenciaTests()
    {
        _sut = new TurnoService(
            _turnoRepo.Object,
            _servicioRepo.Object,
            _clienteRepo.Object,
            _sucursalRepo.Object,
            _emailService.Object,
            _comentarioRepo.Object,
            "http://localhost:3000",
            notificacionRepo: _notificacionRepo.Object);

        _notificacionRepo
            .Setup(r => r.AddAsync(It.IsAny<Notificacion>()))
            .Returns(Task.CompletedTask);
        _notificacionRepo
            .Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);
    }

    // ── Setup helpers ──────────────────────────────────────────────────────────

    private void SetupCrearOk(Servicio servicio, Cliente cliente, Sucursal sucursal)
    {
        _servicioRepo.Setup(r => r.GetByIdConDiasAsync(servicio.Id)).ReturnsAsync(servicio);
        _clienteRepo.Setup(r  => r.GetByIdAsync(cliente.Id)).ReturnsAsync(cliente);
        _sucursalRepo.Setup(r => r.GetByIdConConfiguracionAsync(It.IsAny<Guid>())).ReturnsAsync(sucursal);
        _sucursalRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(sucursal);
        _turnoRepo.Setup(r => r.ExisteConflictoHorarioAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), null))
            .ReturnsAsync(false);
        _turnoRepo.Setup(r => r.AddAsync(It.IsAny<Turno>())).Returns(Task.CompletedTask);
        _turnoRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);
        _turnoRepo.Setup(r => r.GetByIdConRelacionesAsync(It.IsAny<Guid>())).ReturnsAsync((Turno?)null);
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
    // ── CreateAsync ───────────────────────────────────────────────────────────
    // =========================================================================

    [Fact]
    public async Task Create_EstadoConfirmado_PersistNotificacionNuevoTurno()
    {
        var servicio = TurnoTestFactory.CrearServicio();
        var cliente  = TurnoTestFactory.CrearCliente();
        var sucursal = TurnoTestFactory.CrearSucursalConConfig();
        var dto      = TurnoTestFactory.CrearDto(servicioId: servicio.Id, clienteId: cliente.Id);

        SetupCrearOk(servicio, cliente, sucursal);

        await _sut.CreateAsync(dto, OrigenTurno.Administrativo);

        _notificacionRepo.Verify(
            r => r.AddAsync(It.Is<Notificacion>(n =>
                n.Tipo == TipoNotificacion.NuevoTurno &&
                !n.Leida &&
                n.SucursalId == dto.SucursalId)),
            Times.Once,
            "debe persistir una notificación de tipo NuevoTurno cuando el turno queda Confirmado");
    }

    [Fact]
    public async Task Create_EstadoConfirmado_GuardaSaveChanges()
    {
        var servicio = TurnoTestFactory.CrearServicio();
        var cliente  = TurnoTestFactory.CrearCliente();
        var sucursal = TurnoTestFactory.CrearSucursalConConfig();
        var dto      = TurnoTestFactory.CrearDto(servicioId: servicio.Id, clienteId: cliente.Id);

        SetupCrearOk(servicio, cliente, sucursal);

        await _sut.CreateAsync(dto, OrigenTurno.Administrativo);

        _notificacionRepo.Verify(r => r.SaveChangesAsync(), Times.Once,
            "debe llamar SaveChangesAsync para persistir la notificación");
    }

    [Fact]
    public async Task Create_EstadoPendientePago_NoPersistNotificacion()
    {
        var servicio = TurnoTestFactory.CrearServicio(
            requiereSena: true, tipoSena: TipoSena.Fija, valorSena: 1500m);
        var cliente  = TurnoTestFactory.CrearCliente();
        var sucursal = TurnoTestFactory.CrearSucursalConConfig();
        var dto      = TurnoTestFactory.CrearDto(servicioId: servicio.Id, clienteId: cliente.Id);

        SetupCrearOk(servicio, cliente, sucursal);

        await _sut.CreateAsync(dto, OrigenTurno.Web);

        _notificacionRepo.Verify(r => r.AddAsync(It.IsAny<Notificacion>()), Times.Never,
            "no debe persistir si el turno quedó en PendientePago — se notificará al confirmar el pago");
    }

    [Fact]
    public async Task Create_EstadoPendienteModoManual_PersistNotificacion()
    {
        // Turno Pendiente (modo Manual) → SÍ persiste notificación para que el staff vea la solicitud al reabrir el panel
        var servicio = TurnoTestFactory.CrearServicio();
        var cliente  = TurnoTestFactory.CrearCliente();
        var sucursal = TurnoTestFactory.CrearSucursalConConfig(ModoConfirmacionTurnos.Manual);
        var dto      = TurnoTestFactory.CrearDto(servicioId: servicio.Id, clienteId: cliente.Id);

        SetupCrearOk(servicio, cliente, sucursal);

        await _sut.CreateAsync(dto, OrigenTurno.Web);

        _notificacionRepo.Verify(r => r.AddAsync(It.IsAny<Notificacion>()), Times.Once,
            "debe persistir la notificación cuando el turno queda en Pendiente (modo Manual)");
    }

    [Fact]
    public async Task Create_ConObservaciones_MensajeIncluyeTextoDeCliente()
    {
        var servicio = TurnoTestFactory.CrearServicio();
        var cliente  = TurnoTestFactory.CrearCliente();
        var sucursal = TurnoTestFactory.CrearSucursalConConfig();
        var dto      = TurnoTestFactory.CrearDto(servicioId: servicio.Id, clienteId: cliente.Id);
        dto.Observaciones = "Av. Siempre Viva 742";

        SetupCrearOk(servicio, cliente, sucursal);

        Notificacion? capturada = null;
        _notificacionRepo
            .Setup(r => r.AddAsync(It.IsAny<Notificacion>()))
            .Callback<Notificacion>(n => capturada = n)
            .Returns(Task.CompletedTask);

        await _sut.CreateAsync(dto, OrigenTurno.Administrativo);

        capturada.Should().NotBeNull();
        capturada!.Mensaje.Should().Contain("Tiene un mensaje del cliente",
            "cuando hay observaciones el mensaje debe avisar al profesional que hay indicaciones del cliente");
    }

    [Fact]
    public async Task Create_SinObservaciones_MensajeNoIncluyeTextoDeCliente()
    {
        var servicio = TurnoTestFactory.CrearServicio();
        var cliente  = TurnoTestFactory.CrearCliente();
        var sucursal = TurnoTestFactory.CrearSucursalConConfig();
        var dto      = TurnoTestFactory.CrearDto(servicioId: servicio.Id, clienteId: cliente.Id);
        dto.Observaciones = null;

        SetupCrearOk(servicio, cliente, sucursal);

        Notificacion? capturada = null;
        _notificacionRepo
            .Setup(r => r.AddAsync(It.IsAny<Notificacion>()))
            .Callback<Notificacion>(n => capturada = n)
            .Returns(Task.CompletedTask);

        await _sut.CreateAsync(dto, OrigenTurno.Administrativo);

        capturada!.Mensaje.Should().NotContain("mensaje");
    }

    [Fact]
    public async Task Create_RepositorioFalla_NoLanzaException()
    {
        var servicio = TurnoTestFactory.CrearServicio();
        var cliente  = TurnoTestFactory.CrearCliente();
        var sucursal = TurnoTestFactory.CrearSucursalConConfig();
        var dto      = TurnoTestFactory.CrearDto(servicioId: servicio.Id, clienteId: cliente.Id);

        SetupCrearOk(servicio, cliente, sucursal);

        _notificacionRepo
            .Setup(r => r.AddAsync(It.IsAny<Notificacion>()))
            .ThrowsAsync(new Exception("DB no disponible"));

        await _sut.Invoking(s => s.CreateAsync(dto, OrigenTurno.Administrativo))
            .Should().NotThrowAsync(
                "un fallo de persistencia de notificaciones no debe cancelar la creación del turno");
    }

    // =========================================================================
    // ── UpdateEstadoAsync ─────────────────────────────────────────────────────
    // =========================================================================

    [Theory]
    [InlineData(EstadoTurno.CanceladoStaff)]
    [InlineData(EstadoTurno.CanceladoCliente)]
    public async Task Update_Cancelacion_PersistNotificacionCancelacion(EstadoTurno estado)
    {
        var turno = TurnoTestFactory.CrearTurnoConRelaciones();
        var dto   = new UpdateTurnoDto { Estado = estado };

        SetupUpdateOk(turno);

        await _sut.UpdateEstadoAsync(turno.Id, dto);

        _notificacionRepo.Verify(
            r => r.AddAsync(It.Is<Notificacion>(n =>
                n.Tipo == TipoNotificacion.Cancelacion &&
                !n.Leida &&
                n.SucursalId == turno.SucursalId)),
            Times.Once,
            $"debe persistir una notificación de Cancelacion cuando el estado es {estado}");
    }

    [Theory]
    [InlineData(EstadoTurno.Completado)]
    [InlineData(EstadoTurno.NoShow)]
    [InlineData(EstadoTurno.Confirmado)]
    [InlineData(EstadoTurno.Reprogramado)]
    public async Task Update_EstadoNoCancelacion_NoPersistNotificacion(EstadoTurno estado)
    {
        var turno = TurnoTestFactory.CrearTurnoConRelaciones();
        var dto   = new UpdateTurnoDto { Estado = estado };

        SetupUpdateOk(turno);

        await _sut.UpdateEstadoAsync(turno.Id, dto);

        _notificacionRepo.Verify(r => r.AddAsync(It.IsAny<Notificacion>()), Times.Never,
            $"no debe persistir notificación para el estado {estado}");
    }

    [Fact]
    public async Task Update_CancelacionRepositorioFalla_NoLanzaException()
    {
        var turno = TurnoTestFactory.CrearTurnoConRelaciones();
        var dto   = new UpdateTurnoDto { Estado = EstadoTurno.CanceladoStaff };

        SetupUpdateOk(turno);

        _notificacionRepo
            .Setup(r => r.AddAsync(It.IsAny<Notificacion>()))
            .ThrowsAsync(new Exception("DB timeout"));

        await _sut.Invoking(s => s.UpdateEstadoAsync(turno.Id, dto))
            .Should().NotThrowAsync(
                "un fallo de persistencia no debe impedir que la cancelación se procese correctamente");
    }

    // =========================================================================
    // ── CancelarPorTokenAsync ─────────────────────────────────────────────────
    // =========================================================================

    [Fact]
    public async Task CancelarPorToken_PersistNotificacionCancelacion()
    {
        var cliente = TurnoTestFactory.CrearCliente(email: null); // sin email para omitir envíos
        var turno   = TurnoTestFactory.CrearTurnoConRelaciones(cliente: cliente);

        _turnoRepo.Setup(r => r.GetByTokenAsync(turno.TokenGestion)).ReturnsAsync(turno);
        _turnoRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);
        _sucursalRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Sucursal?)null);

        await _sut.CancelarPorTokenAsync(turno.TokenGestion);

        _notificacionRepo.Verify(
            r => r.AddAsync(It.Is<Notificacion>(n =>
                n.Tipo == TipoNotificacion.Cancelacion &&
                n.SucursalId == turno.SucursalId &&
                !n.Leida)),
            Times.Once,
            "la cancelación por token del cliente debe persistir una notificación para que el staff la vea");
    }

    [Fact]
    public async Task CancelarPorToken_MensajeContieneNombreCliente()
    {
        var cliente = TurnoTestFactory.CrearCliente(email: null);
        var turno   = TurnoTestFactory.CrearTurnoConRelaciones(cliente: cliente);

        _turnoRepo.Setup(r => r.GetByTokenAsync(turno.TokenGestion)).ReturnsAsync(turno);
        _turnoRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);
        _sucursalRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Sucursal?)null);

        Notificacion? capturada = null;
        _notificacionRepo
            .Setup(r => r.AddAsync(It.IsAny<Notificacion>()))
            .Callback<Notificacion>(n => capturada = n)
            .Returns(Task.CompletedTask);

        await _sut.CancelarPorTokenAsync(turno.TokenGestion);

        capturada.Should().NotBeNull();
        capturada!.Mensaje.Should().Contain("Juan",
            "el mensaje debe incluir el nombre del cliente para que el staff sepa quién canceló");
        capturada.Mensaje.Should().Contain("cancelado");
    }

    [Fact]
    public async Task CancelarPorToken_RepositorioFalla_NoLanzaException()
    {
        var cliente = TurnoTestFactory.CrearCliente(email: null);
        var turno   = TurnoTestFactory.CrearTurnoConRelaciones(cliente: cliente);

        _turnoRepo.Setup(r => r.GetByTokenAsync(turno.TokenGestion)).ReturnsAsync(turno);
        _turnoRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);
        _sucursalRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Sucursal?)null);

        _notificacionRepo
            .Setup(r => r.AddAsync(It.IsAny<Notificacion>()))
            .ThrowsAsync(new Exception("connection pool exhausted"));

        await _sut.Invoking(s => s.CancelarPorTokenAsync(turno.TokenGestion))
            .Should().NotThrowAsync(
                "la persistencia de notificaciones es no-crítica — no debe bloquear la cancelación del cliente");
    }
}
