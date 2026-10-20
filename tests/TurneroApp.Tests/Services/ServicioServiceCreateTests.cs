using FluentAssertions;
using Moq;
using Xunit;
using TurneroApp.Application.DTOs.Servicio;
using TurneroApp.Application.Interfaces;
using TurneroApp.Application.Services;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;
using TurneroApp.Tests.Helpers;

namespace TurneroApp.Tests.Services;

public class ServicioServiceCreateTests
{
    private readonly Mock<IServicioRepository>         _servicioRepo         = new();
    private readonly Mock<IUsuarioServicioRepository>  _usuarioServicioRepo  = new();
    private readonly Mock<ISuscripcionRepository>      _suscripcionRepo      = new();
    private readonly ServicioService                   _sut;

    public ServicioServiceCreateTests()
    {
        _sut = new ServicioService(
            _servicioRepo.Object,
            _usuarioServicioRepo.Object,
            new SuscripcionGuard(_suscripcionRepo.Object));
    }

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

    private void SetupCrearOk()
    {
        _suscripcionRepo.Setup(r => r.GetBySucursalAsync(It.IsAny<Guid>()))
            .ReturnsAsync((SucursalSuscripcion?)null);

        _servicioRepo.Setup(r => r.AddAsync(It.IsAny<Servicio>())).Returns(Task.CompletedTask);
        _servicioRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);
    }

    [Fact]
    public async Task Crear_SuscripcionActiva_PermiteCrear()
    {
        SetupCrearOk();

        var result = await _sut.CreateAsync(CrearDto());

        result.Should().NotBeNull();
    }
}
