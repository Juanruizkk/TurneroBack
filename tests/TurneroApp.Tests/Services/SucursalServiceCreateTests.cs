using FluentAssertions;
using Moq;
using Xunit;
using TurneroApp.Application.DTOs.Sucursal;
using TurneroApp.Application.Interfaces;
using TurneroApp.Application.Services;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;
using TurneroApp.Tests.Helpers;

namespace TurneroApp.Tests.Services;

public class SucursalServiceCreateTests
{
    private readonly Mock<ISucursalRepository>    _sucursalRepo = new();
    private readonly Mock<ICajaRepository>         _cajaRepo     = new();
    private readonly Mock<IAdjuntoService>         _adjunto      = new();
    private readonly Mock<ISuscripcionRepository>  _suscRepo     = new();
    private readonly SucursalService               _sut;

    public SucursalServiceCreateTests()
    {
        _sucursalRepo.Setup(r => r.GetBySlugAsync(It.IsAny<string>())).ReturnsAsync((Sucursal?)null);
        _sut = new SucursalService(_sucursalRepo.Object, _cajaRepo.Object, _adjunto.Object, _suscRepo.Object);
    }

    private static CreateSucursalDto CrearDto() => new()
    {
        Nombre    = "Sucursal Test",
        Direccion = "Av. Test 123",
        Telefono  = "1234567890",
        Email     = "test@test.com",
    };

    [Fact]
    public async Task CreateAsync_CreaSucursalCajaYSuscripcionTrial()
    {
        var plan = SuscripcionTestFactory.CrearPlan();
        _suscRepo.Setup(r => r.GetPlanBasicoAsync()).ReturnsAsync(plan);

        await _sut.CreateAsync(CrearDto());

        _sucursalRepo.Verify(r => r.AddAsync(It.IsAny<Sucursal>()), Times.Once());
        _cajaRepo.Verify(r => r.AddAsync(It.IsAny<Caja>()), Times.Once());
        _suscRepo.Verify(r => r.AddAsync(It.IsAny<SucursalSuscripcion>()), Times.Once());
    }

    [Fact]
    public async Task CreateAsync_SuscripcionEsTrial()
    {
        var plan = SuscripcionTestFactory.CrearPlan();
        _suscRepo.Setup(r => r.GetPlanBasicoAsync()).ReturnsAsync(plan);

        SucursalSuscripcion? suscripcionCapturada = null;
        _suscRepo
            .Setup(r => r.AddAsync(It.IsAny<SucursalSuscripcion>()))
            .Callback<SucursalSuscripcion>(s => suscripcionCapturada = s);

        await _sut.CreateAsync(CrearDto());

        suscripcionCapturada.Should().NotBeNull();
        suscripcionCapturada!.Estado.Should().Be(EstadoSuscripcion.Activa);
        suscripcionCapturada.EnPeriodoPrueba.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_FechaVencimiento_Correcta()
    {
        var plan = SuscripcionTestFactory.CrearPlan(/*DuracionTrialDias = 14*/);
        _suscRepo.Setup(r => r.GetPlanBasicoAsync()).ReturnsAsync(plan);

        SucursalSuscripcion? suscripcionCapturada = null;
        _suscRepo
            .Setup(r => r.AddAsync(It.IsAny<SucursalSuscripcion>()))
            .Callback<SucursalSuscripcion>(s => suscripcionCapturada = s);

        var antes = DateTime.UtcNow;
        await _sut.CreateAsync(CrearDto());
        var despues = DateTime.UtcNow;

        suscripcionCapturada.Should().NotBeNull();
        var esperadoMin = antes.AddYears(100);
        var esperadoMax = despues.AddYears(100);

        suscripcionCapturada!.FechaVencimiento.Should().BeOnOrAfter(esperadoMin).And.BeOnOrBefore(esperadoMax);
    }

    [Fact]
    public async Task CreateAsync_SinPlanBase_LanzaException()
    {
        _suscRepo.Setup(r => r.GetPlanBasicoAsync()).ReturnsAsync((Plan?)null);

        var act = async () => await _sut.CreateAsync(CrearDto());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*plan base*");
    }

    [Fact]
    public async Task CreateAsync_SaveChangesAsync_LlamadoUnaVez()
    {
        var plan = SuscripcionTestFactory.CrearPlan();
        _suscRepo.Setup(r => r.GetPlanBasicoAsync()).ReturnsAsync(plan);

        await _sut.CreateAsync(CrearDto());

        _sucursalRepo.Verify(r => r.SaveChangesAsync(), Times.Once());
    }
}
