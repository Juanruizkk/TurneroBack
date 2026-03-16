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

public class TurnoServiceCreateTests
{
    private readonly Mock<ITurnoRepository>           _turnoRepo        = new();
    private readonly Mock<IServicioRepository>        _servicioRepo     = new();
    private readonly Mock<IClienteRepository>         _clienteRepo      = new();
    private readonly Mock<ISucursalRepository>        _sucursalRepo     = new();
    private readonly Mock<IEmailService>              _emailService     = new();
    private readonly Mock<IComentarioTurnoRepository> _comentarioRepo   = new();
    private readonly TurnoService                     _sut;

    public TurnoServiceCreateTests()
    {
        _sut = new TurnoService(
            _turnoRepo.Object,
            _servicioRepo.Object,
            _clienteRepo.Object,
            _sucursalRepo.Object,
            _emailService.Object,
            _comentarioRepo.Object,
            "http://localhost:3000");
    }

    // ── Setup helpers ──────────────────────────────────────────────────────────

    /// <summary>Configura todos los mocks para un flujo CreateAsync sin errores.</summary>
    private void SetupCrearOk(Servicio servicio, Cliente cliente, Sucursal sucursal)
    {
        _servicioRepo.Setup(r => r.GetByIdAsync(servicio.Id)).ReturnsAsync(servicio);
        _clienteRepo.Setup(r  => r.GetByIdAsync(cliente.Id)).ReturnsAsync(cliente);

        _turnoRepo.Setup(r => r.ExisteConflictoHorarioAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), null))
            .ReturnsAsync(false);

        _sucursalRepo.Setup(r => r.GetByIdConConfiguracionAsync(It.IsAny<Guid>()))
            .ReturnsAsync(sucursal);
        _sucursalRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(sucursal);

        _turnoRepo.Setup(r => r.AddAsync(It.IsAny<Turno>())).Returns(Task.CompletedTask);
        _turnoRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        // Devuelve null para que el servicio use el turno recién creado directamente
        _turnoRepo.Setup(r => r.GetByIdConRelacionesAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Turno?)null);

        _emailService.Setup(e => e.EnviarConfirmacionTurnoAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ServicioNoEncontrado_LanzaException()
    {
        _servicioRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Servicio?)null);

        var dto = TurnoTestFactory.CrearDto();

        await _sut.Invoking(s => s.CreateAsync(dto))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Create_ClienteNoEncontrado_LanzaException()
    {
        var servicio = TurnoTestFactory.CrearServicio();
        _servicioRepo.Setup(r => r.GetByIdAsync(servicio.Id)).ReturnsAsync(servicio);
        _clienteRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Cliente?)null);

        var dto = TurnoTestFactory.CrearDto(servicioId: servicio.Id);

        await _sut.Invoking(s => s.CreateAsync(dto))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Create_ConflictoHorario_LanzaException()
    {
        var servicio = TurnoTestFactory.CrearServicio();
        var cliente  = TurnoTestFactory.CrearCliente();

        _servicioRepo.Setup(r => r.GetByIdAsync(servicio.Id)).ReturnsAsync(servicio);
        _clienteRepo.Setup(r  => r.GetByIdAsync(cliente.Id)).ReturnsAsync(cliente);
        _turnoRepo.Setup(r => r.ExisteConflictoHorarioAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), null))
            .ReturnsAsync(true);

        var dto = TurnoTestFactory.CrearDto(servicioId: servicio.Id, clienteId: cliente.Id);

        var ex = await _sut.Invoking(s => s.CreateAsync(dto))
            .Should().ThrowAsync<InvalidOperationException>();
        ex.WithMessage("*horario*");
    }

    [Fact]
    public async Task Create_SinConflicto_RetornaDto()
    {
        var servicio = TurnoTestFactory.CrearServicio();
        var cliente  = TurnoTestFactory.CrearCliente();
        var sucursal = TurnoTestFactory.CrearSucursalConConfig();
        var dto      = TurnoTestFactory.CrearDto(servicioId: servicio.Id, clienteId: cliente.Id);

        SetupCrearOk(servicio, cliente, sucursal);

        var result = await _sut.CreateAsync(dto);

        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Create_CalculaFechaHoraFin_Correctamente()
    {
        var servicio = TurnoTestFactory.CrearServicio(duracionAgendaMin: 45);
        var cliente  = TurnoTestFactory.CrearCliente();
        var sucursal = TurnoTestFactory.CrearSucursalConConfig();
        var inicio   = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var dto      = TurnoTestFactory.CrearDto(servicioId: servicio.Id, clienteId: cliente.Id);
        dto.FechaHoraInicio = inicio;

        SetupCrearOk(servicio, cliente, sucursal);

        var result = await _sut.CreateAsync(dto);

        result.FechaHoraFin.Should().Be(inicio.AddMinutes(45));
    }

    [Fact]
    public async Task Create_SinSena_MontoSenaEsNull()
    {
        var servicio = TurnoTestFactory.CrearServicio(requiereSena: false);
        var cliente  = TurnoTestFactory.CrearCliente();
        var sucursal = TurnoTestFactory.CrearSucursalConConfig();
        var dto      = TurnoTestFactory.CrearDto(servicioId: servicio.Id, clienteId: cliente.Id);

        SetupCrearOk(servicio, cliente, sucursal);

        var result = await _sut.CreateAsync(dto);

        result.MontoSena.Should().BeNull();
    }

    [Fact]
    public async Task Create_SenaFija_MontoSenaIgualAlValor()
    {
        var servicio = TurnoTestFactory.CrearServicio(
            precio: 10000m, requiereSena: true,
            tipoSena: TipoSena.Fija, valorSena: 2000m);
        var cliente  = TurnoTestFactory.CrearCliente();
        var sucursal = TurnoTestFactory.CrearSucursalConConfig();
        var dto      = TurnoTestFactory.CrearDto(servicioId: servicio.Id, clienteId: cliente.Id);

        SetupCrearOk(servicio, cliente, sucursal);

        var result = await _sut.CreateAsync(dto);

        result.MontoSena.Should().Be(2000m);
    }

    [Fact]
    public async Task Create_SenaPorcentaje_CalculaCorrectamente()
    {
        var servicio = TurnoTestFactory.CrearServicio(
            precio: 10000m, requiereSena: true,
            tipoSena: TipoSena.Porcentaje, valorSena: 30m);
        var cliente  = TurnoTestFactory.CrearCliente();
        var sucursal = TurnoTestFactory.CrearSucursalConConfig();
        var dto      = TurnoTestFactory.CrearDto(servicioId: servicio.Id, clienteId: cliente.Id);

        SetupCrearOk(servicio, cliente, sucursal);

        var result = await _sut.CreateAsync(dto);

        result.MontoSena.Should().Be(3000m);
    }

    [Fact]
    public async Task Create_OrigenWeb_ModoManual_EstadoPendiente()
    {
        var servicio = TurnoTestFactory.CrearServicio();
        var cliente  = TurnoTestFactory.CrearCliente();
        var sucursal = TurnoTestFactory.CrearSucursalConConfig(ModoConfirmacionTurnos.Manual);
        var dto      = TurnoTestFactory.CrearDto(
            servicioId: servicio.Id, clienteId: cliente.Id,
            origen: OrigenTurno.Web);

        SetupCrearOk(servicio, cliente, sucursal);

        var result = await _sut.CreateAsync(dto);

        result.Estado.Should().Be(EstadoTurno.Pendiente);
    }

    [Fact]
    public async Task Create_OrigenWeb_ModoAutomatico_EstadoConfirmado()
    {
        var servicio = TurnoTestFactory.CrearServicio();
        var cliente  = TurnoTestFactory.CrearCliente();
        var sucursal = TurnoTestFactory.CrearSucursalConConfig(ModoConfirmacionTurnos.Automatico);
        var dto      = TurnoTestFactory.CrearDto(
            servicioId: servicio.Id, clienteId: cliente.Id,
            origen: OrigenTurno.Web);

        SetupCrearOk(servicio, cliente, sucursal);

        var result = await _sut.CreateAsync(dto);

        result.Estado.Should().Be(EstadoTurno.Confirmado);
    }

    [Fact]
    public async Task Create_OrigenAdministrativo_SiempreConfirmado()
    {
        var servicio = TurnoTestFactory.CrearServicio();
        var cliente  = TurnoTestFactory.CrearCliente();
        // Aunque el modo sea Manual, el origen Administrativo siempre confirma
        var sucursal = TurnoTestFactory.CrearSucursalConConfig(ModoConfirmacionTurnos.Manual);
        var dto      = TurnoTestFactory.CrearDto(
            servicioId: servicio.Id, clienteId: cliente.Id,
            origen: OrigenTurno.Administrativo);

        SetupCrearOk(servicio, cliente, sucursal);

        var result = await _sut.CreateAsync(dto);

        result.Estado.Should().Be(EstadoTurno.Confirmado);
    }

    [Fact]
    public async Task Create_TokenGestion_EsHexDe32Caracteres()
    {
        var servicio = TurnoTestFactory.CrearServicio();
        var cliente  = TurnoTestFactory.CrearCliente();
        var sucursal = TurnoTestFactory.CrearSucursalConConfig();
        var dto      = TurnoTestFactory.CrearDto(servicioId: servicio.Id, clienteId: cliente.Id);

        SetupCrearOk(servicio, cliente, sucursal);

        var result = await _sut.CreateAsync(dto);

        result.TokenGestion.Should().HaveLength(32);
        result.TokenGestion.Should().MatchRegex("^[0-9a-f]{32}$");
    }

    [Fact]
    public async Task Create_ClienteConEmail_EnviaEmailConfirmacion()
    {
        var servicio = TurnoTestFactory.CrearServicio();
        var cliente  = TurnoTestFactory.CrearCliente("cliente@test.com");
        var sucursal = TurnoTestFactory.CrearSucursalConConfig();
        var dto      = TurnoTestFactory.CrearDto(servicioId: servicio.Id, clienteId: cliente.Id);

        SetupCrearOk(servicio, cliente, sucursal);

        await _sut.CreateAsync(dto);

        _emailService.Verify(e => e.EnviarConfirmacionTurnoAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task Create_ClienteSinEmail_NoEnviaEmail()
    {
        var servicio = TurnoTestFactory.CrearServicio();
        var cliente  = TurnoTestFactory.CrearCliente(email: null);
        var sucursal = TurnoTestFactory.CrearSucursalConConfig();
        var dto      = TurnoTestFactory.CrearDto(servicioId: servicio.Id, clienteId: cliente.Id);

        SetupCrearOk(servicio, cliente, sucursal);

        await _sut.CreateAsync(dto);

        _emailService.Verify(e => e.EnviarConfirmacionTurnoAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Create_EmailFalla_NoLanzaException()
    {
        var servicio = TurnoTestFactory.CrearServicio();
        var cliente  = TurnoTestFactory.CrearCliente("cliente@test.com");
        var sucursal = TurnoTestFactory.CrearSucursalConConfig();
        var dto      = TurnoTestFactory.CrearDto(servicioId: servicio.Id, clienteId: cliente.Id);

        SetupCrearOk(servicio, cliente, sucursal);

        _emailService.Setup(e => e.EnviarConfirmacionTurnoAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("SMTP error"));

        await _sut.Invoking(s => s.CreateAsync(dto))
            .Should().NotThrowAsync();
    }
}
