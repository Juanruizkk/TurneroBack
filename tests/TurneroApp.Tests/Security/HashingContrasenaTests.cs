using BCrypt.Net;
using FluentAssertions;
using Moq;
using TurneroApp.Application.DTOs.SuperAdmin;
using TurneroApp.Application.Interfaces;
using TurneroApp.Application.Services;
using TurneroApp.Domain.Entities;
using Xunit;

namespace TurneroApp.Tests.Security;

/// <summary>
/// Tests unitarios para verificar el correcto hashing de contraseñas con BCrypt.
/// Hallazgos cubiertos: C-03 (SincronizarPassword), C-04 (CrearConOwner).
/// </summary>
public class HashingContrasenaTests
{
    private readonly Mock<ISucursalRepository>           _sucursalRepo    = new();
    private readonly Mock<IUsuarioRepository>            _usuarioRepo     = new();
    private readonly Mock<IUsuarioSucursalRolRepository> _usrRepo         = new();
    private readonly Mock<ICajaRepository>               _cajaRepo        = new();
    private readonly Mock<ISuscripcionRepository>        _suscripcionRepo = new();
    private readonly SuperAdminService                    _sut;

    public HashingContrasenaTests()
    {
        _sut = new SuperAdminService(
            _sucursalRepo.Object,
            _usuarioRepo.Object,
            _usrRepo.Object,
            _cajaRepo.Object,
            _suscripcionRepo.Object);
    }

    /// <summary>
    /// Test 20 — CrearConOwnerAsync debe guardar la contraseña del owner hasheada con BCrypt.
    /// Hallazgos C-03 + C-04: previene almacenar contraseñas en texto plano.
    /// </summary>
    [Fact]
    public async Task CrearConOwner_PasswordOwner_SeGuardaHasheada()
    {
        // Arrange
        const string passwordPlano = "ClaveTextoPlano123!";

        var dto = new CrearSucursalConOwnerDto
        {
            Nombre        = "Sucursal Test",
            Direccion     = "Calle 123",
            Telefono      = "1111111111",
            Email         = "sucursal@test.com",
            OwnerNombre   = "Juan",
            OwnerApellido = "Test",
            OwnerEmail    = "owner@test.com",
            OwnerTelefono = "2222222222",
            OwnerPassword = passwordPlano,
        };

        // Capturar el Usuario añadido para inspeccionarlo luego
        Usuario? ownerCapturado = null;

        _sucursalRepo.Setup(r => r.AddAsync(It.IsAny<Sucursal>()))
                     .Returns(Task.CompletedTask);
        _usuarioRepo.Setup(r => r.GetByEmailGlobalAsync("owner@test.com"))
                    .ReturnsAsync((Usuario?)null);
        _usuarioRepo.Setup(r => r.AddAsync(It.IsAny<Usuario>()))
                    .Callback<Usuario>(u => ownerCapturado = u)
                    .Returns(Task.CompletedTask);
        _usrRepo.Setup(r => r.AddAsync(It.IsAny<UsuarioSucursalRol>()))
                .Returns(Task.CompletedTask);
        _cajaRepo.Setup(r => r.AddAsync(It.IsAny<Caja>()))
                 .Returns(Task.CompletedTask);
        _sucursalRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        await _sut.CrearConOwnerAsync(dto);

        // Assert — la contraseña guardada NO es texto plano pero BCrypt la reconoce
        ownerCapturado.Should().NotBeNull("el usuario owner debe haber sido añadido");
        ownerCapturado!.PasswordHash.Should().NotBe(passwordPlano,
            "la contraseña nunca debe guardarse en texto plano");
        BCrypt.Net.BCrypt.Verify(passwordPlano, ownerCapturado.PasswordHash).Should().BeTrue(
            "la contraseña debe guardarse con BCrypt hash, nunca en texto plano");
    }

    /// <summary>
    /// Test 21 — SincronizarPasswordAsync debe guardar la nueva contraseña hasheada. (C-03)
    /// El hash almacenado debe ser verificable por BCrypt pero distinto del texto plano.
    /// </summary>
    [Fact]
    public async Task SincronizarPassword_NuevaContrasena_SeGuardaHasheada()
    {
        // Arrange
        const string nuevaClave = "NuevaClave!";
        var usuario = new Usuario
        {
            Id           = Guid.NewGuid(),
            Nombre       = "Test",
            Apellido     = "User",
            Email        = "u@t.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("claveVieja"),
            FechaCreacion = DateTime.UtcNow,
        };

        _usuarioRepo.Setup(r => r.GetByEmailGlobalAsync("u@t.com"))
                    .ReturnsAsync(usuario);
        _usuarioRepo.Setup(r => r.Update(It.IsAny<Usuario>()));
        _usuarioRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        await _sut.SincronizarPasswordAsync("u@t.com", nuevaClave);

        // Assert
        usuario.PasswordHash.Should().NotBe(nuevaClave,
            "la contraseña nunca debe guardarse en texto plano");
        BCrypt.Net.BCrypt.Verify(nuevaClave, usuario.PasswordHash).Should().BeTrue(
            "el hash almacenado debe corresponder a la nueva contraseña");
    }
}
