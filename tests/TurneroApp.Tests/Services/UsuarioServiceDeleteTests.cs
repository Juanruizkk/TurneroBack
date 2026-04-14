using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using TurneroApp.Application.Interfaces;
using TurneroApp.Application.Services;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;
using TurneroApp.Tests.Helpers;

namespace TurneroApp.Tests.Services;

public class UsuarioServiceDeleteTests
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

    public UsuarioServiceDeleteTests()
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

    // ── Helpers ────────────────────────────────────────────────────────────────

    private void SetupSinTurnosFuturos()
        => _turnoRepo.Setup(r => r.TieneTurnosFuturosActivosAsync(It.IsAny<Guid>()))
                     .ReturnsAsync(false);

    private void SetupConTurnosFuturos()
        => _turnoRepo.Setup(r => r.TieneTurnosFuturosActivosAsync(It.IsAny<Guid>()))
                     .ReturnsAsync(true);

    // ── DeleteAsync (soft delete) ──────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_UsuarioNoEncontrado_LanzaException()
    {
        // Arrange
        _usrRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((UsuarioSucursalRol?)null);

        // Act & Assert
        await _sut.Invoking(s => s.DeleteAsync(Guid.NewGuid()))
                  .Should().ThrowAsync<InvalidOperationException>()
                  .WithMessage("*No se encontró*");
    }

    [Fact]
    public async Task DeleteAsync_Owner_LanzaException()
    {
        // Arrange
        var usr = UsuarioTestFactory.CrearUSROwner();
        _usrRepo.Setup(r => r.GetByIdAsync(usr.Id)).ReturnsAsync(usr);

        // Act & Assert
        await _sut.Invoking(s => s.DeleteAsync(usr.Id))
                  .Should().ThrowAsync<InvalidOperationException>()
                  .WithMessage("*dueño*");
    }

    [Fact]
    public async Task DeleteAsync_TieneTurnosFuturos_LanzaException()
    {
        // Arrange
        var usr = UsuarioTestFactory.CrearUSR();
        _usrRepo.Setup(r => r.GetByIdAsync(usr.Id)).ReturnsAsync(usr);
        SetupConTurnosFuturos();

        // Act & Assert
        await _sut.Invoking(s => s.DeleteAsync(usr.Id))
                  .Should().ThrowAsync<InvalidOperationException>()
                  .WithMessage("*turnos futuros*");
    }

    [Fact]
    public async Task DeleteAsync_UsuarioValido_MarcaEstadoInactivo()
    {
        // Arrange
        var usr = UsuarioTestFactory.CrearUSR();
        _usrRepo.Setup(r => r.GetByIdAsync(usr.Id)).ReturnsAsync(usr);
        _usrRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        SetupSinTurnosFuturos();

        // Act
        await _sut.DeleteAsync(usr.Id);

        // Assert
        usr.Estado.Should().Be(EstadoUsuario.Inactivo);
        _usrRepo.Verify(r => r.Update(usr), Times.Once());
        _usrRepo.Verify(r => r.SaveChangesAsync(), Times.Once());
    }

    [Fact]
    public async Task DeleteAsync_UsuarioValido_NoConsultaTurnosSiEsOwner()
    {
        // Arrange — owner debe bloquearse antes de consultar turnos
        var usr = UsuarioTestFactory.CrearUSROwner();
        _usrRepo.Setup(r => r.GetByIdAsync(usr.Id)).ReturnsAsync(usr);

        // Act
        await _sut.Invoking(s => s.DeleteAsync(usr.Id))
                  .Should().ThrowAsync<InvalidOperationException>();

        // Assert — no debe haberse consultado el repo de turnos
        _turnoRepo.Verify(r => r.TieneTurnosFuturosActivosAsync(It.IsAny<Guid>()), Times.Never());
    }

    // ── HardDeleteAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task HardDeleteAsync_UsuarioNoEncontrado_LanzaException()
    {
        // Arrange
        _usrRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((UsuarioSucursalRol?)null);

        // Act & Assert
        await _sut.Invoking(s => s.HardDeleteAsync(Guid.NewGuid()))
                  .Should().ThrowAsync<InvalidOperationException>()
                  .WithMessage("*no encontrado*");
    }

    [Fact]
    public async Task HardDeleteAsync_Owner_LanzaException()
    {
        // Arrange
        var usr = UsuarioTestFactory.CrearUSROwner();
        _usrRepo.Setup(r => r.GetByIdAsync(usr.Id)).ReturnsAsync(usr);

        // Act & Assert
        await _sut.Invoking(s => s.HardDeleteAsync(usr.Id))
                  .Should().ThrowAsync<InvalidOperationException>()
                  .WithMessage("*dueño*");
    }

    [Fact]
    public async Task HardDeleteAsync_TieneTurnosFuturos_LanzaException()
    {
        // Arrange
        var usr = UsuarioTestFactory.CrearUSR();
        _usrRepo.Setup(r => r.GetByIdAsync(usr.Id)).ReturnsAsync(usr);
        SetupConTurnosFuturos();

        // Act & Assert
        await _sut.Invoking(s => s.HardDeleteAsync(usr.Id))
                  .Should().ThrowAsync<InvalidOperationException>()
                  .WithMessage("*turnos futuros*");
    }

    [Fact]
    public async Task HardDeleteAsync_UsuarioValido_LlamaDeleteHardAsync()
    {
        // Arrange
        var usr = UsuarioTestFactory.CrearUSRPendiente();
        _usrRepo.Setup(r => r.GetByIdAsync(usr.Id)).ReturnsAsync(usr);
        _usrRepo.Setup(r => r.DeleteHardAsync(usr.Id)).Returns(Task.CompletedTask);
        SetupSinTurnosFuturos();

        // Act
        await _sut.HardDeleteAsync(usr.Id);

        // Assert
        _usrRepo.Verify(r => r.DeleteHardAsync(usr.Id), Times.Once());
    }

    [Fact]
    public async Task HardDeleteAsync_UsuarioValido_NoLlamaSoftDelete()
    {
        // Arrange
        var usr = UsuarioTestFactory.CrearUSRPendiente();
        _usrRepo.Setup(r => r.GetByIdAsync(usr.Id)).ReturnsAsync(usr);
        _usrRepo.Setup(r => r.DeleteHardAsync(usr.Id)).Returns(Task.CompletedTask);
        SetupSinTurnosFuturos();

        // Act
        await _sut.HardDeleteAsync(usr.Id);

        // Assert — no debe haber tocado Update ni SaveChangesAsync del soft delete
        _usrRepo.Verify(r => r.Update(It.IsAny<UsuarioSucursalRol>()), Times.Never());
        _usrRepo.Verify(r => r.SaveChangesAsync(), Times.Never());
    }

    [Fact]
    public async Task HardDeleteAsync_Owner_NoConsultaTurnosSiEsOwner()
    {
        // Arrange — owner debe bloquearse antes de consultar turnos
        var usr = UsuarioTestFactory.CrearUSROwner();
        _usrRepo.Setup(r => r.GetByIdAsync(usr.Id)).ReturnsAsync(usr);

        // Act
        await _sut.Invoking(s => s.HardDeleteAsync(usr.Id))
                  .Should().ThrowAsync<InvalidOperationException>();

        // Assert
        _turnoRepo.Verify(r => r.TieneTurnosFuturosActivosAsync(It.IsAny<Guid>()), Times.Never());
    }
}
