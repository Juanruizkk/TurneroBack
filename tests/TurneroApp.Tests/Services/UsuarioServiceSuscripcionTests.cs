using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using TurneroApp.Application.Exceptions;
using TurneroApp.Application.Interfaces;
using TurneroApp.Application.Services;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;
using TurneroApp.Tests.Helpers;

namespace TurneroApp.Tests.Services;

public class UsuarioServiceSuscripcionTests
{
    private readonly Mock<IUsuarioRepository>            _usuarioRepo         = new();
    private readonly Mock<IUsuarioSucursalRolRepository> _usrRepo             = new();
    private readonly Mock<ISucursalRepository>           _sucursalRepo        = new();
    private readonly Mock<IEmailService>                 _emailService        = new();
    private readonly Mock<IHorarioLaboralRepository>     _horarioRepo         = new();
    private readonly Mock<IUsuarioServicioRepository>    _usuarioServicioRepo = new();
    private readonly Mock<ITurnoRepository>              _turnoRepo           = new();
    private readonly Mock<ISuscripcionRepository>        _suscripcionRepo     = new();

    private UsuarioService BuildSut() => new UsuarioService(
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

    [Fact]
    public async Task CreateAsync_SuscripcionVencida_LanzaSuscripcionVencidaException()
    {
        _suscripcionRepo.Setup(r => r.GetBySucursalAsync(It.IsAny<Guid>()))
            .ReturnsAsync(SuscripcionTestFactory.CrearSuscripcion(estado: EstadoSuscripcion.PruebVencida));

        var sut = BuildSut();
        var dto = UsuarioTestFactory.CrearCreateDto();
        var act = async () => await sut.CreateAsync(dto);

        await act.Should().ThrowAsync<SuscripcionVencidaException>();
    }

    [Fact]
    public async Task CreateAsync_SuscripcionVencida_NoLlamaAddAsync()
    {
        _suscripcionRepo.Setup(r => r.GetBySucursalAsync(It.IsAny<Guid>()))
            .ReturnsAsync(SuscripcionTestFactory.CrearSuscripcion(estado: EstadoSuscripcion.PruebVencida));

        var sut = BuildSut();
        var dto = UsuarioTestFactory.CrearCreateDto();
        try { await sut.CreateAsync(dto); } catch { }

        _usrRepo.Verify(r => r.AddAsync(It.IsAny<UsuarioSucursalRol>()), Times.Never());
    }
}
