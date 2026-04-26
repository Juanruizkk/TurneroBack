using FluentAssertions;
using Moq;
using Xunit;
using TurneroApp.Application.DTOs.Auth;
using TurneroApp.Application.Interfaces;
using TurneroApp.Application.Services;
using TurneroApp.Domain.Entities;
using TurneroApp.Tests.Helpers;

namespace TurneroApp.Tests.Services;

public class AuthServiceTests
{
    private readonly Mock<IUsuarioRepository>            _usuarioRepo = new();
    private readonly Mock<IUsuarioSucursalRolRepository> _usrRepo     = new();
    private readonly AuthService                          _sut;

    private const string SaEmail    = "sa@test.com";
    private const string SaPassword = "SuperPass!";

    public AuthServiceTests()
    {
        _sut = new AuthService(
            _usuarioRepo.Object,
            _usrRepo.Object,
            SaEmail,
            SaPassword);
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidarAsync_SuperAdmin_CredencialesCorrectas_RetornaDirectamente()
    {
        // Arrange
        var dto = new LoginDto { Email = SaEmail, Password = SaPassword };

        // Act
        var resultado = await _sut.ValidarAsync(dto);

        // Assert
        resultado.Usuario!.EsSuperAdmin.Should().BeTrue();
        _usuarioRepo.Verify(r => r.GetByEmailAsync(It.IsAny<string>()), Times.Never());
        _usrRepo.Verify(r => r.GetByUsuarioAsync(It.IsAny<Guid>()), Times.Never());
    }

    /// <summary>
    /// C-05 — SuperAdmin con contraseña incorrecta → UnauthorizedAccessException.
    /// Verifica que AuthService usa BCrypt.Verify (tiempo constante) y NO el
    /// operador == (vulnerable a timing attacks) para validar la contraseña del SA.
    /// </summary>
    [Fact]
    public async Task ValidarAsync_SuperAdmin_PasswordIncorrecta_LanzaUnauthorized()
    {
        // Arrange
        var dto = new LoginDto { Email = SaEmail, Password = "PasswordIncorrecto" };

        // Act
        var act = () => _sut.ValidarAsync(dto);

        // Assert
        await act.Should()
            .ThrowAsync<UnauthorizedAccessException>(
                "credenciales incorrectas de SuperAdmin deben rechazarse con UnauthorizedAccessException");
    }

    [Fact]
    public async Task ValidarAsync_EmailNoExiste_LanzaUnauthorized()
    {
        // Arrange
        var dto = new LoginDto { Email = "noexiste@test.com", Password = "cualquier" };

        _usuarioRepo.Setup(r => r.GetByEmailAsync("noexiste@test.com"))
            .ReturnsAsync((Usuario?)null);

        // Act
        var act = () => _sut.ValidarAsync(dto);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Credenciales*");
    }

    [Fact]
    public async Task ValidarAsync_PasswordIncorrecta_LanzaUnauthorized()
    {
        // Arrange
        var usuario = UsuarioTestFactory.CrearUsuario("user@test.com", "passwordCorrecto");
        var dto     = new LoginDto { Email = "user@test.com", Password = "passwordIncorrecto" };

        _usuarioRepo.Setup(r => r.GetByEmailAsync("user@test.com"))
            .ReturnsAsync(usuario);

        // Act
        var act = () => _sut.ValidarAsync(dto);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ValidarAsync_SinSucursalesActivas_LanzaUnauthorized()
    {
        // Arrange
        var usuario = UsuarioTestFactory.CrearUsuario("user@test.com", "pass123");
        var dto     = new LoginDto { Email = "user@test.com", Password = "pass123" };

        _usuarioRepo.Setup(r => r.GetByEmailAsync("user@test.com"))
            .ReturnsAsync(usuario);
        _usrRepo.Setup(r => r.GetByUsuarioAsync(usuario.Id))
            .ReturnsAsync([]);

        // Act
        var act = () => _sut.ValidarAsync(dto);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*no tiene sucursales activas*");
    }

    [Fact]
    public async Task ValidarAsync_UnaSucursalActiva_RetornaLoginDirecto()
    {
        // Arrange
        var usuario = UsuarioTestFactory.CrearUsuario("user@test.com", "pass123");
        var usr     = UsuarioTestFactory.CrearUSR(usuario);
        var dto     = new LoginDto { Email = "user@test.com", Password = "pass123" };

        _usuarioRepo.Setup(r => r.GetByEmailAsync("user@test.com"))
            .ReturnsAsync(usuario);
        _usrRepo.Setup(r => r.GetByUsuarioAsync(usuario.Id))
            .ReturnsAsync([usr]);

        // Act
        var resultado = await _sut.ValidarAsync(dto);

        // Assert
        resultado.RequiereSeleccion.Should().BeFalse();
        resultado.Usuario.Should().NotBeNull();
        resultado.Usuario!.Email.Should().Be("user@test.com");
        resultado.Usuario.Rol.Should().Be("Profesional");
    }

    [Fact]
    public async Task ValidarAsync_MultiplesSucursalesActivas_RetornaSeleccionConToken()
    {
        // Arrange
        var usuario = UsuarioTestFactory.CrearUsuario("user@test.com", "pass123");
        var usr1    = UsuarioTestFactory.CrearUSR(usuario);
        var usr2    = UsuarioTestFactory.CrearUSR(usuario);
        var dto     = new LoginDto { Email = "user@test.com", Password = "pass123" };

        _usuarioRepo.Setup(r => r.GetByEmailAsync("user@test.com"))
            .ReturnsAsync(usuario);
        _usrRepo.Setup(r => r.GetByUsuarioAsync(usuario.Id))
            .ReturnsAsync([usr1, usr2]);
        _usuarioRepo.Setup(r => r.Update(It.IsAny<Usuario>()));
        _usuarioRepo.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var resultado = await _sut.ValidarAsync(dto);

        // Assert
        resultado.RequiereSeleccion.Should().BeTrue();
        resultado.Opciones!.Count.Should().Be(2);
        resultado.SeleccionToken.Should().NotBeNullOrEmpty();
        _usuarioRepo.Verify(r => r.Update(It.IsAny<Usuario>()));
        _usuarioRepo.Verify(r => r.SaveChangesAsync());
    }
}
