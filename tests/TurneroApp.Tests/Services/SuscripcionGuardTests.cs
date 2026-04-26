using FluentAssertions;
using Moq;
using Xunit;
using TurneroApp.Application.Exceptions;
using TurneroApp.Application.Interfaces;
using TurneroApp.Application.Services;
using TurneroApp.Domain.Enums;
using TurneroApp.Tests.Helpers;

namespace TurneroApp.Tests.Services;

public class SuscripcionGuardTests
{
    private readonly Mock<ISuscripcionRepository> _repo = new();
    private readonly SuscripcionGuard             _sut;

    public SuscripcionGuardTests()
    {
        _sut = new SuscripcionGuard(_repo.Object);
    }

    private void SetupSuscripcion(Domain.Entities.SucursalSuscripcion? s) =>
        _repo.Setup(r => r.GetBySucursalAsync(It.IsAny<Guid>())).ReturnsAsync(s);

    // ─── VerificarActivaAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task VerificarActiva_SinSuscripcion_NoLanzaExcepcion()
    {
        // Sin registro en DB → sucursal legacy → acceso libre
        SetupSuscripcion(null);

        var act = () => _sut.VerificarActivaAsync(Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task VerificarActiva_EstadoActiva_NoLanzaExcepcion()
    {
        SetupSuscripcion(SuscripcionTestFactory.CrearSuscripcion(estado: EstadoSuscripcion.Activa));

        var act = () => _sut.VerificarActivaAsync(Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task VerificarActiva_EstadoPruebActiva_NoLanzaExcepcion()
    {
        SetupSuscripcion(SuscripcionTestFactory.CrearSuscripcion(estado: EstadoSuscripcion.PruebActiva));

        var act = () => _sut.VerificarActivaAsync(Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task VerificarActiva_EstadoPruebVencida_LanzaSuscripcionVencidaException()
    {
        SetupSuscripcion(SuscripcionTestFactory.CrearSuscripcion(estado: EstadoSuscripcion.PruebVencida));

        var act = () => _sut.VerificarActivaAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<SuscripcionVencidaException>()
            .WithMessage("*período de prueba venció*");
    }

    [Fact]
    public async Task VerificarActiva_EstadoVencida_LanzaSuscripcionVencidaException()
    {
        // FechaVencimiento debe estar más allá del período de gracia (3 días por defecto)
        // para que el guard no devuelva sin lanzar
        SetupSuscripcion(SuscripcionTestFactory.CrearSuscripcion(
            estado: EstadoSuscripcion.Vencida,
            fechaVencimiento: DateTime.UtcNow.AddDays(-10)));

        var act = () => _sut.VerificarActivaAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<SuscripcionVencidaException>()
            .WithMessage("*venció*");
    }

    [Fact]
    public async Task VerificarActiva_EstadoCancelada_LanzaSuscripcionVencidaException()
    {
        SetupSuscripcion(SuscripcionTestFactory.CrearSuscripcion(estado: EstadoSuscripcion.Cancelada));

        var act = () => _sut.VerificarActivaAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<SuscripcionVencidaException>()
            .WithMessage("*cancelada*");
    }

    // ─── VerificarTieneReportesAsync ──────────────────────────────────────────

    [Fact]
    public async Task VerificarReportes_SinSuscripcion_NoLanzaExcepcion()
    {
        // Sin registro → legacy → acceso libre
        SetupSuscripcion(null);

        var act = () => _sut.VerificarTieneReportesAsync(Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task VerificarReportes_PlanConReportes_NoLanzaExcepcion()
    {
        var plan = SuscripcionTestFactory.CrearPlan(tieneReportes: true, nombre: "Pro");
        SetupSuscripcion(SuscripcionTestFactory.CrearSuscripcion(plan: plan));

        var act = () => _sut.VerificarTieneReportesAsync(Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task VerificarReportes_PlanSinReportes_LanzaFeatureNoPlanException()
    {
        var plan = SuscripcionTestFactory.CrearPlan(tieneReportes: false, nombre: "Básico");
        SetupSuscripcion(SuscripcionTestFactory.CrearSuscripcion(plan: plan));

        var act = () => _sut.VerificarTieneReportesAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<FeatureNoPlanException>()
            .WithMessage("*Básico*");
    }
}
