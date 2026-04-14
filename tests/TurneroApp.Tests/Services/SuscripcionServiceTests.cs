using FluentAssertions;
using Moq;
using Xunit;
using TurneroApp.Application.Interfaces;
using TurneroApp.Application.Services;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;
using TurneroApp.Tests.Helpers;

namespace TurneroApp.Tests.Services;

public class SuscripcionServiceTests
{
    private readonly Mock<ISuscripcionRepository>         _repo         = new();
    private readonly Mock<ISucursalRepository>            _sucursalRepo = new();
    private readonly Mock<IUsuarioSucursalRolRepository>  _usrRepo      = new();
    private readonly Mock<ITurnoRepository>               _turnoRepo    = new();
    private readonly SuscripcionService                   _sut;

    public SuscripcionServiceTests()
    {
        _usrRepo.Setup(r => r.ContarActivosBySucursalAsync(It.IsAny<Guid>())).ReturnsAsync(0);
        _turnoRepo.Setup(r => r.ContarPorSucursalEnMesAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(0);

        _sut = new SuscripcionService(
            _repo.Object,
            _sucursalRepo.Object,
            _usrRepo.Object,
            _turnoRepo.Object);
    }

    // ─── Sin suscripción + sin planes → SinSuscripcion permisivo ─────────────

    [Fact]
    public async Task ObtenerAsync_SinSuscripcionYSinPlanes_RetornaEstadoSinSuscripcion()
    {
        // La sucursal no tiene suscripción Y no hay planes en la DB
        _repo.Setup(r => r.GetBySucursalAsync(It.IsAny<Guid>()))
             .ReturnsAsync((SucursalSuscripcion?)null);
        _repo.Setup(r => r.GetPlanBasicoAsync())
             .ReturnsAsync((Plan?)null);

        var result = await _sut.ObtenerAsync(Guid.NewGuid());

        result.Estado.Should().Be("SinSuscripcion");
        result.MaxUsuarios.Should().Be(999); // permisivo
    }

    // ─── Estado vigente → no debe haber transición ───────────────────────────

    [Fact]
    public async Task ObtenerAsync_PruebActiva_NoVencida_MantieneEstado()
    {
        var suscripcion = SuscripcionTestFactory.CrearSuscripcion(
            estado:           EstadoSuscripcion.PruebActiva,
            fechaVencimiento: DateTime.UtcNow.AddDays(5));

        _repo.Setup(r => r.GetBySucursalAsync(It.IsAny<Guid>()))
             .ReturnsAsync(suscripcion);

        var result = await _sut.ObtenerAsync(Guid.NewGuid());

        result.Estado.Should().Be("PruebActiva");
        _repo.Verify(r => r.Update(It.IsAny<SucursalSuscripcion>()), Times.Never());
    }

    // ─── Transición lazy PruebActiva → PruebVencida ───────────────────────────

    [Fact]
    public async Task ObtenerAsync_PruebActiva_YaVencida_TransicionaPruebVencida()
    {
        var suscripcion = SuscripcionTestFactory.CrearSuscripcion(
            estado:           EstadoSuscripcion.PruebActiva,
            fechaVencimiento: DateTime.UtcNow.AddDays(-1)); // ya expiró

        _repo.Setup(r => r.GetBySucursalAsync(It.IsAny<Guid>()))
             .ReturnsAsync(suscripcion);
        _repo.Setup(r => r.SaveChangesAsync())
             .ReturnsAsync(0);

        var result = await _sut.ObtenerAsync(Guid.NewGuid());

        result.Estado.Should().Be("PruebVencida");
        _repo.Verify(r => r.Update(It.Is<SucursalSuscripcion>(s => s.Estado == EstadoSuscripcion.PruebVencida)), Times.Once());
        _repo.Verify(r => r.SaveChangesAsync(), Times.Once());
    }

    // ─── Transición lazy Activa → Vencida ────────────────────────────────────

    [Fact]
    public async Task ObtenerAsync_Activa_YaVencida_TransicionaVencida()
    {
        var suscripcion = SuscripcionTestFactory.CrearSuscripcion(
            estado:           EstadoSuscripcion.Activa,
            fechaVencimiento: DateTime.UtcNow.AddDays(-1),  // ya expiró
            enPeriodoPrueba:  false);

        _repo.Setup(r => r.GetBySucursalAsync(It.IsAny<Guid>()))
             .ReturnsAsync(suscripcion);
        _repo.Setup(r => r.SaveChangesAsync())
             .ReturnsAsync(0);

        var result = await _sut.ObtenerAsync(Guid.NewGuid());

        result.Estado.Should().Be("Vencida");
        _repo.Verify(r => r.Update(It.Is<SucursalSuscripcion>(s => s.Estado == EstadoSuscripcion.Vencida)), Times.Once());
        _repo.Verify(r => r.SaveChangesAsync(), Times.Once());
    }
}
