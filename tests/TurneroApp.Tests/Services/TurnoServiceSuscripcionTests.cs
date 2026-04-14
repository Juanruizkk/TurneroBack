using FluentAssertions;
using Moq;
using Xunit;
using TurneroApp.Application.Exceptions;
using TurneroApp.Application.Interfaces;
using TurneroApp.Application.Services;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;
using TurneroApp.Tests.Helpers;

namespace TurneroApp.Tests.Services;

public class TurnoServiceSuscripcionTests
{
    private readonly Mock<ITurnoRepository>           _turnoRepo       = new();
    private readonly Mock<IServicioRepository>        _servicioRepo    = new();
    private readonly Mock<IClienteRepository>         _clienteRepo     = new();
    private readonly Mock<ISucursalRepository>        _sucursalRepo    = new();
    private readonly Mock<IEmailService>              _emailService    = new();
    private readonly Mock<IComentarioTurnoRepository> _comentarioRepo  = new();
    private readonly Mock<ISuscripcionRepository>     _suscripcionRepo = new();

    private TurnoService BuildSut() => new TurnoService(
        _turnoRepo.Object,
        _servicioRepo.Object,
        _clienteRepo.Object,
        _sucursalRepo.Object,
        _emailService.Object,
        _comentarioRepo.Object,
        "http://localhost:3000",
        suscripcionRepo: _suscripcionRepo.Object,
        suscripcionGuard: new SuscripcionGuard(_suscripcionRepo.Object));

    [Fact]
    public async Task CreateAsync_SuscripcionVencida_LanzaSuscripcionVencidaException()
    {
        _suscripcionRepo.Setup(r => r.GetBySucursalAsync(It.IsAny<Guid>()))
            .ReturnsAsync(SuscripcionTestFactory.CrearSuscripcion(estado: EstadoSuscripcion.PruebVencida));

        var sut = BuildSut();
        var dto = TurnoTestFactory.CrearDto();
        var act = async () => await sut.CreateAsync(dto);

        await act.Should().ThrowAsync<SuscripcionVencidaException>();
    }

    [Fact]
    public async Task CreateAsync_SuscripcionVencida_NoLlamaAddAsync()
    {
        _suscripcionRepo.Setup(r => r.GetBySucursalAsync(It.IsAny<Guid>()))
            .ReturnsAsync(SuscripcionTestFactory.CrearSuscripcion(estado: EstadoSuscripcion.PruebVencida));

        var sut = BuildSut();
        var dto = TurnoTestFactory.CrearDto();
        try { await sut.CreateAsync(dto); } catch { }

        _turnoRepo.Verify(r => r.AddAsync(It.IsAny<Turno>()), Times.Never());
    }
}
