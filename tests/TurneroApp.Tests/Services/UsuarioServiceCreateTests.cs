using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using TurneroApp.Application.DTOs.Usuario;
using TurneroApp.Application.Interfaces;
using TurneroApp.Application.Services;
using TurneroApp.Domain.Entities;
using TurneroApp.Tests.Helpers;

namespace TurneroApp.Tests.Services;

public class UsuarioServiceCreateTests
{
    private readonly Mock<IUsuarioRepository>            _usuarioRepo         = new();
    private readonly Mock<IUsuarioSucursalRolRepository> _usrRepo             = new();
    private readonly Mock<ISucursalRepository>           _sucursalRepo        = new();
    private readonly Mock<IEmailService>                 _emailService        = new();
    private readonly Mock<IHorarioLaboralRepository>     _horarioRepo         = new();
    private readonly Mock<IUsuarioServicioRepository>    _usuarioServicioRepo = new();
    private readonly Mock<ITurnoRepository>              _turnoRepo           = new();
    private readonly Mock<ISuscripcionRepository>        _suscripcionRepo     = new();
    private readonly UsuarioService                       _sut;

    public UsuarioServiceCreateTests()
    {
        _sut = new UsuarioService(
            _usuarioRepo.Object,
            _usrRepo.Object,
            _sucursalRepo.Object,
            _emailService.Object,
            "http://localhost:3000",
            _horarioRepo.Object,
            _usuarioServicioRepo.Object,
            _turnoRepo.Object,
            _suscripcionRepo.Object,
            new SuscripcionGuard(_suscripcionRepo.Object),
            Mock.Of<ILogger<UsuarioService>>());
    }

    // ── Setup helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Configura los mocks para un flujo CreateAsync de usuario nuevo sin errores.
    /// Devuelve el usuario capturado en el callback de AddAsync para poder inspeccionarlo.
    /// </summary>
    private (CreateUsuarioDto dto, Sucursal sucursal, Func<Usuario?> getUsuarioCapturado)
        SetupEmailNuevoOk()
    {
        var sucursal = UsuarioTestFactory.CrearSucursal();
        var dto      = UsuarioTestFactory.CrearCreateDto(sucursalId: sucursal.Id);

        _sucursalRepo.Setup(r => r.GetByIdAsync(dto.SucursalId))
            .ReturnsAsync(sucursal);
        _suscripcionRepo.Setup(r => r.GetBySucursalAsync(It.IsAny<Guid>()))
            .ReturnsAsync((SucursalSuscripcion?)null);
        _usuarioRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((Usuario?)null);

        Usuario? usuarioCapturado = null;

        _usuarioRepo.Setup(r => r.AddAsync(It.IsAny<Usuario>()))
            .Callback<Usuario>(u => usuarioCapturado = u)
            .Returns(Task.CompletedTask);

        _usrRepo.Setup(r => r.AddAsync(It.IsAny<UsuarioSucursalRol>()))
            .Callback<UsuarioSucursalRol>(usr =>
            {
                // Inyectar nav props para evitar NullReferenceException en MapToResponse
                usr.Usuario  = usuarioCapturado!;
                usr.Sucursal = sucursal;
            })
            .Returns(Task.CompletedTask);

        _usrRepo.Setup(r => r.SaveChangesAsync())
            .Returns(Task.CompletedTask);

        _emailService.Setup(e => e.EnviarInvitacionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        return (dto, sucursal, () => usuarioCapturado);
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_EmailNuevo_CreaUsuarioYUSR()
    {
        // Arrange
        var (dto, _, _) = SetupEmailNuevoOk();

        // Act
        await _sut.CreateAsync(dto);

        // Assert
        _usuarioRepo.Verify(r => r.AddAsync(It.IsAny<Usuario>()), Times.Once());
        _usrRepo.Verify(r => r.AddAsync(It.IsAny<UsuarioSucursalRol>()), Times.Once());
    }

    [Fact]
    public async Task CreateAsync_EmailNuevo_EnviaEmailEstandar()
    {
        // Arrange
        var (dto, _, _) = SetupEmailNuevoOk();

        // Act
        await _sut.CreateAsync(dto);

        // Assert
        _emailService.Verify(e => e.EnviarInvitacionAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once());
        _emailService.Verify(e => e.EnviarInvitacionUsuarioExistenteAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never());
    }

    [Fact]
    public async Task CreateAsync_EmailNuevo_PasswordHashEsVacio()
    {
        // Arrange
        var (dto, _, getUsuarioCapturado) = SetupEmailNuevoOk();

        // Act
        await _sut.CreateAsync(dto);

        // Assert
        getUsuarioCapturado()!.PasswordHash.Should().Be(string.Empty);
    }

    [Fact]
    public async Task CreateAsync_EmailExistente_SoloCreaUSR_YEnviaEmailExistente()
    {
        // Arrange
        var usuarioExistente = UsuarioTestFactory.CrearUsuario("existente@test.com");
        var sucursal         = UsuarioTestFactory.CrearSucursal();
        var dto              = UsuarioTestFactory.CrearCreateDto(
            sucursalId: sucursal.Id,
            email: "existente@test.com");

        _sucursalRepo.Setup(r => r.GetByIdAsync(dto.SucursalId))
            .ReturnsAsync(sucursal);
        _usuarioRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(usuarioExistente);
        _usrRepo.Setup(r => r.GetByUsuarioYSucursalAsync(usuarioExistente.Id, dto.SucursalId))
            .ReturnsAsync((UsuarioSucursalRol?)null);

        _usrRepo.Setup(r => r.AddAsync(It.IsAny<UsuarioSucursalRol>()))
            .Callback<UsuarioSucursalRol>(usr =>
            {
                // El usuario ya existe; inyectar nav prop directamente
                usr.Usuario  = usuarioExistente;
                usr.Sucursal = sucursal;
            })
            .Returns(Task.CompletedTask);

        _usrRepo.Setup(r => r.SaveChangesAsync())
            .Returns(Task.CompletedTask);
        _emailService.Setup(e => e.EnviarInvitacionUsuarioExistenteAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.CreateAsync(dto);

        // Assert
        _usuarioRepo.Verify(r => r.AddAsync(It.IsAny<Usuario>()), Times.Never());
        _emailService.Verify(e => e.EnviarInvitacionUsuarioExistenteAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once());
        _emailService.Verify(e => e.EnviarInvitacionAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never());
    }

    // ── Límite de usuarios por plan ────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_SinSuscripcion_PermiteCrearUsuario()
    {
        // Arrange
        var (dto, _, _) = SetupEmailNuevoOk();
        // _suscripcionRepo ya devuelve null desde SetupEmailNuevoOk → sin restricción

        // Act
        await _sut.CreateAsync(dto);

        // Assert
        _usrRepo.Verify(r => r.AddAsync(It.IsAny<UsuarioSucursalRol>()), Times.Once());
    }

    [Fact]
    public async Task CreateAsync_ConPlanActivo_DebajoDeLimite_PermiteCrear()
    {
        // Arrange
        var plan        = SuscripcionTestFactory.CrearPlan(maxUsuarios: 3);
        var suscripcion = SuscripcionTestFactory.CrearSuscripcion(plan: plan);
        var sucursal    = UsuarioTestFactory.CrearSucursal();
        var dto         = UsuarioTestFactory.CrearCreateDto(sucursalId: sucursal.Id);

        suscripcion.SucursalId = sucursal.Id;

        _suscripcionRepo.Setup(r => r.GetBySucursalAsync(sucursal.Id))
            .ReturnsAsync(suscripcion);
        _usrRepo.Setup(r => r.ContarActivosBySucursalAsync(sucursal.Id))
            .ReturnsAsync(2); // 2 < MaxUsuarios(3) → permite

        _sucursalRepo.Setup(r => r.GetByIdAsync(sucursal.Id)).ReturnsAsync(sucursal);
        _usuarioRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((Usuario?)null);
        _usuarioRepo.Setup(r => r.AddAsync(It.IsAny<Usuario>())).Returns(Task.CompletedTask);
        _usrRepo.Setup(r => r.AddAsync(It.IsAny<UsuarioSucursalRol>()))
            .Callback<UsuarioSucursalRol>(usr =>
            {
                usr.Usuario  = new Usuario { Nombre = dto.Nombre, Apellido = dto.Apellido, Email = dto.Email };
                usr.Sucursal = sucursal;
            })
            .Returns(Task.CompletedTask);
        _usrRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _emailService.Setup(e => e.EnviarInvitacionAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.CreateAsync(dto);

        // Assert
        _usrRepo.Verify(r => r.AddAsync(It.IsAny<UsuarioSucursalRol>()), Times.Once());
    }

    [Fact]
    public async Task CreateAsync_ConPlanActivo_LimiteSuperado_LanzaException()
    {
        // Arrange
        var plan        = SuscripcionTestFactory.CrearPlan(maxUsuarios: 2, nombre: "Básico");
        var suscripcion = SuscripcionTestFactory.CrearSuscripcion(plan: plan);
        var sucursal    = UsuarioTestFactory.CrearSucursal();
        var dto         = UsuarioTestFactory.CrearCreateDto(sucursalId: sucursal.Id);

        suscripcion.SucursalId = sucursal.Id;

        _suscripcionRepo.Setup(r => r.GetBySucursalAsync(sucursal.Id))
            .ReturnsAsync(suscripcion);
        _usrRepo.Setup(r => r.ContarActivosBySucursalAsync(sucursal.Id))
            .ReturnsAsync(2); // 2 >= MaxUsuarios(2) → bloquea

        // Act
        var act = () => _sut.CreateAsync(dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Básico*");
        _usrRepo.Verify(r => r.AddAsync(It.IsAny<UsuarioSucursalRol>()), Times.Never());
    }

    [Fact]
    public async Task CreateAsync_EmailExistente_YaMiembroDeSucursal_LanzaException()
    {
        // Arrange
        var usuarioExistente = UsuarioTestFactory.CrearUsuario("existente@test.com");
        var usrExistente     = UsuarioTestFactory.CrearUSR(usuarioExistente);
        var dto              = UsuarioTestFactory.CrearCreateDto(
            sucursalId: usrExistente.SucursalId,
            email: "existente@test.com");

        _sucursalRepo.Setup(r => r.GetByIdAsync(dto.SucursalId))
            .ReturnsAsync(usrExistente.Sucursal);
        _usuarioRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(usuarioExistente);
        _usrRepo.Setup(r => r.GetByUsuarioYSucursalAsync(usuarioExistente.Id, dto.SucursalId))
            .ReturnsAsync(usrExistente);

        // Act
        var act = () => _sut.CreateAsync(dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ya es miembro*");
    }
}
