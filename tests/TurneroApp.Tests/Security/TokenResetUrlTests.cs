using FluentAssertions;
using Moq;
using TurneroApp.Application.Interfaces;
using TurneroApp.Application.Services;
using TurneroApp.Tests.Helpers;
using Xunit;

namespace TurneroApp.Tests.Security;

/// <summary>
/// Test unitario para la URL de recuperación de contraseña (B-04).
/// Verifica que el token se envía en el fragment (#token=...) y no en el
/// query param (?token=...), evitando que quede registrado en access logs
/// del servidor o en el header Referer de terceros.
/// </summary>
public class TokenResetUrlTests
{
    private readonly Mock<IUsuarioRepository> _usuarioRepo  = new();
    private readonly Mock<IEmailService>      _emailService = new();
    private readonly RecuperacionContrasenaService _sut;

    public TokenResetUrlTests()
    {
        _sut = new RecuperacionContrasenaService(
            _usuarioRepo.Object,
            _emailService.Object,
            "https://app.metaturnos.com");
    }

    /// <summary>
    /// Test 25 — El link de recuperación debe usar #token= (fragment HTTP) y
    /// NO ?token= (query param).
    ///
    /// Los fragments nunca se envían al servidor en requests HTTP:
    ///   - No aparecen en access logs del servidor
    ///   - No se incluyen en el header Referer hacia analytics/CDN
    ///   - Solo son leídos por el frontend: window.location.hash.slice(1)
    /// </summary>
    [Fact]
    public async Task SolicitarRecuperacion_LinkEnEmail_UsaFragment()
    {
        // Arrange
        var usuario = UsuarioTestFactory.CrearUsuario("user@t.com", "pass");
        _usuarioRepo
            .Setup(r => r.GetByEmailGlobalAsync("user@t.com"))
            .ReturnsAsync(usuario);

        _usuarioRepo.Setup(r => r.Update(It.IsAny<TurneroApp.Domain.Entities.Usuario>()));
        _usuarioRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        string? linkCapturado = null;
        _emailService
            .Setup(e => e.EnviarRecuperacionContrasenaAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, link) => linkCapturado = link)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SolicitarAsync("user@t.com");

        // Assert
        linkCapturado.Should().NotBeNull(
            "debe enviar un email con un link de recuperación");

        linkCapturado!.Should().Contain("#token=",
            "el token debe ir en el fragment para no aparecer en logs ni Referer");

        linkCapturado.Should().NotContain("?token=",
            "el query param expone el token en access logs del servidor");
    }

    /// <summary>
    /// Test 25b — Email no existente → no envía email (silencioso).
    /// El servicio no debe revelar si el email existe o no.
    /// </summary>
    [Fact]
    public async Task SolicitarRecuperacion_EmailNoExistente_NoEnviaEmail()
    {
        _usuarioRepo
            .Setup(r => r.GetByEmailGlobalAsync(It.IsAny<string>()))
            .ReturnsAsync((TurneroApp.Domain.Entities.Usuario?)null);

        await _sut.SolicitarAsync("noexiste@test.com");

        _emailService.Verify(
            e => e.EnviarRecuperacionContrasenaAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never,
            "no debe revelar si el email existe o no");
    }
}
