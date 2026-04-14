using FluentAssertions;
using Moq;
using Xunit;
using TurneroApp.Application.DTOs.Servicio;
using TurneroApp.Application.Exceptions;
using TurneroApp.Application.Interfaces;
using TurneroApp.Application.Services;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;
using TurneroApp.Tests.Helpers;

namespace TurneroApp.Tests.Services;

public class ServicioServiceSuscripcionTests
{
    private readonly Mock<IServicioRepository>    _servicioRepo    = new();
    private readonly Mock<ISuscripcionRepository> _suscripcionRepo = new();

    private ServicioService BuildSut() => new ServicioService(
        _servicioRepo.Object,
        new SuscripcionGuard(_suscripcionRepo.Object));

    private static CreateServicioDto CrearDto(Guid? sucursalId = null) => new()
    {
        SucursalId          = sucursalId ?? Guid.NewGuid(),
        Nombre              = "Servicio Test",
        Descripcion         = string.Empty,
        Aclaraciones        = string.Empty,
        Precio              = 1000m,
        DuracionClienteMin  = 30,
        DuracionAgendaMin   = 30,
        ModalidadPrestacion = ModalidadPrestacion.Presencial,
        TipoServicio        = TipoServicio.Regular,
    };

    [Fact]
    public async Task CreateAsync_SuscripcionVencida_LanzaSuscripcionVencidaException()
    {
        _suscripcionRepo.Setup(r => r.GetBySucursalAsync(It.IsAny<Guid>()))
            .ReturnsAsync(SuscripcionTestFactory.CrearSuscripcion(estado: EstadoSuscripcion.PruebVencida));

        var sut = BuildSut();
        var act = async () => await sut.CreateAsync(CrearDto());

        await act.Should().ThrowAsync<SuscripcionVencidaException>();
    }

    [Fact]
    public async Task CreateAsync_SuscripcionVencida_NoLlamaAddAsync()
    {
        _suscripcionRepo.Setup(r => r.GetBySucursalAsync(It.IsAny<Guid>()))
            .ReturnsAsync(SuscripcionTestFactory.CrearSuscripcion(estado: EstadoSuscripcion.PruebVencida));

        var sut = BuildSut();
        try { await sut.CreateAsync(CrearDto()); } catch { }

        _servicioRepo.Verify(r => r.AddAsync(It.IsAny<Servicio>()), Times.Never());
    }
}
