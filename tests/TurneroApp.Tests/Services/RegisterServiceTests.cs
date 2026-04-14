using FluentAssertions;
using Moq;
using Xunit;
using TurneroApp.Application.DTOs.Auth;
using TurneroApp.Application.Interfaces;
using TurneroApp.Application.Services;
using TurneroApp.Domain.Entities;

namespace TurneroApp.Tests.Services;

public class RegisterServiceTests
{
    private readonly Mock<IUsuarioRepository>            _usuarioRepo     = new();
    private readonly Mock<IUsuarioSucursalRolRepository> _usrRepo         = new();
    private readonly Mock<ISucursalRepository>           _sucursalRepo    = new();
    private readonly Mock<ICajaRepository>               _cajaRepo        = new();
    private readonly Mock<ISuscripcionRepository>        _suscripcionRepo  = new();
    private readonly Mock<IEmailService>                 _emailService    = new();
    private readonly RegisterService                      _sut;

    public RegisterServiceTests()
    {
        _sut = new RegisterService(
            _usuarioRepo.Object,
            _usrRepo.Object,
            _sucursalRepo.Object,
            _cajaRepo.Object,
            _suscripcionRepo.Object,
            _emailService.Object,
            "https://app.test");

        // Setup comunes para que el registro no falle en los pasos no relevantes
        _usuarioRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((Usuario?)null);
        _sucursalRepo.Setup(r => r.GetBySlugAsync(It.IsAny<string>()))
            .ReturnsAsync((Sucursal?)null);
        _sucursalRepo.Setup(r => r.AddAsync(It.IsAny<Sucursal>()))
            .Returns(Task.CompletedTask);
        _usuarioRepo.Setup(r => r.AddAsync(It.IsAny<Usuario>()))
            .Returns(Task.CompletedTask);
        _usrRepo.Setup(r => r.AddAsync(It.IsAny<Domain.Entities.UsuarioSucursalRol>()))
            .Returns(Task.CompletedTask);
        _cajaRepo.Setup(r => r.AddAsync(It.IsAny<Caja>()))
            .Returns(Task.CompletedTask);
        _suscripcionRepo.Setup(r => r.GetPlanBasicoAsync())
            .ReturnsAsync((Plan?)null);
        _sucursalRepo.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);
    }

    [Fact]
    public async Task RegistrarAsync_GuardaPasswordHasheada_NoTextoPlano()
    {
        // Arrange
        var password = "MiPassword123!";
        var dto = new RegisterDto
        {
            Nombre        = "Juan",
            Apellido      = "Pérez",
            Email         = "juan@test.com",
            Password      = password,
            NombreNegocio = "Mi Negocio",
        };

        Usuario? usuarioGuardado = null;
        _usuarioRepo.Setup(r => r.AddAsync(It.IsAny<Usuario>()))
            .Callback<Usuario>(u => usuarioGuardado = u)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.RegistrarAsync(dto);

        // Assert
        usuarioGuardado.Should().NotBeNull();
        usuarioGuardado!.PasswordHash.Should().NotBe(password, "debe estar hasheada, no en texto plano");
        usuarioGuardado.PasswordHash.Should().StartWith("$2a$", "BCrypt produce hashes que empiezan con $2a$");
    }

    [Fact]
    public async Task RegistrarAsync_HashGenerado_PasaBCryptVerify()
    {
        // Arrange
        var password = "MiPassword123!";
        var dto = new RegisterDto
        {
            Nombre        = "Juan",
            Apellido      = "Pérez",
            Email         = "juan@test.com",
            Password      = password,
            NombreNegocio = "Mi Negocio",
        };

        Usuario? usuarioGuardado = null;
        _usuarioRepo.Setup(r => r.AddAsync(It.IsAny<Usuario>()))
            .Callback<Usuario>(u => usuarioGuardado = u)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.RegistrarAsync(dto);

        // Assert
        BCrypt.Net.BCrypt.Verify(password, usuarioGuardado!.PasswordHash)
            .Should().BeTrue("el hash debe verificar la contraseña original");
    }

    [Fact]
    public async Task RegistrarAsync_EmailYaExiste_LanzaInvalidOperationException()
    {
        // Arrange
        var usuarioExistente = new Usuario { Id = Guid.NewGuid(), Email = "juan@test.com" };
        _usuarioRepo.Setup(r => r.GetByEmailAsync("juan@test.com"))
            .ReturnsAsync(usuarioExistente);

        var dto = new RegisterDto
        {
            Nombre        = "Juan",
            Apellido      = "Pérez",
            Email         = "juan@test.com",
            Password      = "cualquier",
            NombreNegocio = "Mi Negocio",
        };

        // Act
        var act = () => _sut.RegistrarAsync(dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*email ya está registrado*");
    }
}
