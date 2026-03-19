using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using TurneroApp.Api.Services;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;
using TurneroApp.Tests.Helpers;

namespace TurneroApp.Tests.Services;

/// <summary>
/// Suite de tests unitarios para <see cref="TurnoPagoExpirationService"/>.
///
/// NOTA DE DISEÑO: el método <c>ExpirarTurnosAbandonadosAsync</c> se declara <c>internal</c>
/// para poder invocarlo directamente desde los tests, evitando el delay de 15 minutos
/// del bucle del BackgroundService.
/// </summary>
public class TurnoPagoExpirationServiceTests
{
    // ── Mocks ─────────────────────────────────────────────────────────────────

    private readonly Mock<IServiceScopeFactory>                    _scopeFactoryMock;
    private readonly Mock<IServiceScope>                           _scopeMock;
    private readonly Mock<IServiceProvider>                        _serviceProviderMock;
    private readonly Mock<ITurnoRepository>                        _turnoRepoMock;
    private readonly Mock<ILogger<TurnoPagoExpirationService>>     _loggerMock;

    private readonly TurnoPagoExpirationService _sut;

    // ── Constructor (setup compartido) ────────────────────────────────────────

    public TurnoPagoExpirationServiceTests()
    {
        _scopeFactoryMock    = new Mock<IServiceScopeFactory>();
        _scopeMock           = new Mock<IServiceScope>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _turnoRepoMock       = new Mock<ITurnoRepository>();
        _loggerMock          = new Mock<ILogger<TurnoPagoExpirationService>>();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(ITurnoRepository)))
            .Returns(_turnoRepoMock.Object);

        _scopeMock
            .Setup(s => s.ServiceProvider)
            .Returns(_serviceProviderMock.Object);

        _scopeFactoryMock
            .Setup(f => f.CreateScope())
            .Returns(_scopeMock.Object);

        _sut = new TurnoPagoExpirationService(_scopeFactoryMock.Object, _loggerMock.Object);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private Task InvocarExpirarAsync()
        => _sut.ExpirarTurnosAbandonadosAsync();

    // =========================================================================
    // ── Sin turnos vencidos ───────────────────────────────────────────────────
    // =========================================================================

    [Fact]
    public async Task Expirar_SinTurnosVencidos_NoLlamaAUpdateRange()
    {
        _turnoRepoMock
            .Setup(r => r.GetPendientePagoVencidosAsync(It.IsAny<DateTime>()))
            .ReturnsAsync([]);

        await InvocarExpirarAsync();

        _turnoRepoMock.Verify(
            r => r.UpdateRange(It.IsAny<IEnumerable<Turno>>()),
            Times.Never,
            "si no hay turnos abandonados no debe llamar a UpdateRange");
    }

    [Fact]
    public async Task Expirar_SinTurnosVencidos_NoLlamaASaveChanges()
    {
        _turnoRepoMock
            .Setup(r => r.GetPendientePagoVencidosAsync(It.IsAny<DateTime>()))
            .ReturnsAsync([]);

        await InvocarExpirarAsync();

        _turnoRepoMock.Verify(
            r => r.SaveChangesAsync(),
            Times.Never,
            "no debe persistir nada si no hay turnos que expirar");
    }

    // =========================================================================
    // ── Con turnos vencidos — estado ──────────────────────────────────────────
    // =========================================================================

    [Fact]
    public async Task Expirar_ConTurnosVencidos_CambiaEstadoAPagoRechazado()
    {
        // Turnos que llevan más de 30 min en PendientePago sin que el cliente pagara
        var turno1 = TurnoTestFactory.CrearTurnoConRelaciones();
        var turno2 = TurnoTestFactory.CrearTurnoConRelaciones();
        turno1.Estado = EstadoTurno.PendientePago;
        turno2.Estado = EstadoTurno.PendientePago;

        _turnoRepoMock
            .Setup(r => r.GetPendientePagoVencidosAsync(It.IsAny<DateTime>()))
            .ReturnsAsync([turno1, turno2]);
        _turnoRepoMock.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        await InvocarExpirarAsync();

        turno1.Estado.Should().Be(EstadoTurno.PagoRechazado,
            "un turno PendientePago abandonado debe expirar a PagoRechazado para liberar el slot");
        turno2.Estado.Should().Be(EstadoTurno.PagoRechazado);
    }

    // =========================================================================
    // ── Con turnos vencidos — llamadas al repositorio ─────────────────────────
    // =========================================================================

    [Fact]
    public async Task Expirar_ConTurnosVencidos_LlamaUpdateRangeUnaVez()
    {
        var turnos = new[]
        {
            TurnoTestFactory.CrearTurnoConRelaciones(),
            TurnoTestFactory.CrearTurnoConRelaciones(),
        };
        _turnoRepoMock
            .Setup(r => r.GetPendientePagoVencidosAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(turnos);
        _turnoRepoMock.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        await InvocarExpirarAsync();

        _turnoRepoMock.Verify(
            r => r.UpdateRange(It.IsAny<IEnumerable<Turno>>()),
            Times.Once,
            "todos los turnos deben actualizarse en un solo UpdateRange (batch)");
    }

    [Fact]
    public async Task Expirar_ConTurnosVencidos_LlamaSaveChangesUnaVez()
    {
        _turnoRepoMock
            .Setup(r => r.GetPendientePagoVencidosAsync(It.IsAny<DateTime>()))
            .ReturnsAsync([TurnoTestFactory.CrearTurnoConRelaciones()]);
        _turnoRepoMock.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        await InvocarExpirarAsync();

        _turnoRepoMock.Verify(
            r => r.SaveChangesAsync(),
            Times.Once,
            "todos los cambios deben persistirse en una sola llamada");
    }

    [Fact]
    public async Task Expirar_FecheLimite_EsAnteriorA30Minutos()
    {
        DateTime? fechaCapturada = null;
        var antes = DateTime.Now;

        _turnoRepoMock
            .Setup(r => r.GetPendientePagoVencidosAsync(It.IsAny<DateTime>()))
            .Callback<DateTime>(f => fechaCapturada = f)
            .ReturnsAsync([]);

        await InvocarExpirarAsync();

        fechaCapturada.Should().NotBeNull();
        fechaCapturada!.Value.Should().BeBefore(antes.AddMinutes(-29),
            "la fecha límite debe ser al menos 30 minutos antes del momento actual");
        fechaCapturada.Value.Should().BeAfter(antes.AddMinutes(-31),
            "la fecha límite no debe ser excesivamente anterior a los 30 minutos");
    }

    // =========================================================================
    // ── Manejo de errores ─────────────────────────────────────────────────────
    // =========================================================================

    [Fact]
    public async Task Expirar_CuandoRepoLanzaExcepcion_NoPropagarElError()
    {
        _turnoRepoMock
            .Setup(r => r.GetPendientePagoVencidosAsync(It.IsAny<DateTime>()))
            .ThrowsAsync(new InvalidOperationException("Fallo de DB simulado"));

        var act = () => InvocarExpirarAsync();

        await act.Should().NotThrowAsync(
            "el servicio debe absorber la excepción sin derruir el host");
    }

    [Fact]
    public async Task Expirar_CuandoSaveChangesLanzaExcepcion_NoPropagarElError()
    {
        _turnoRepoMock
            .Setup(r => r.GetPendientePagoVencidosAsync(It.IsAny<DateTime>()))
            .ReturnsAsync([TurnoTestFactory.CrearTurnoConRelaciones()]);
        _turnoRepoMock
            .Setup(r => r.SaveChangesAsync())
            .ThrowsAsync(new Exception("Timeout simulado"));

        var act = () => InvocarExpirarAsync();

        await act.Should().NotThrowAsync(
            "cualquier error de persistencia debe ser absorbido por el try/catch externo");
    }

    // =========================================================================
    // ── Ciclo de vida del scope ───────────────────────────────────────────────
    // =========================================================================

    [Fact]
    public async Task Expirar_SiempreCreaExactamenteUnScope()
    {
        _turnoRepoMock
            .Setup(r => r.GetPendientePagoVencidosAsync(It.IsAny<DateTime>()))
            .ReturnsAsync([]);

        await InvocarExpirarAsync();

        _scopeFactoryMock.Verify(f => f.CreateScope(), Times.Once,
            "cada ejecución debe crear un único scope DI");
    }

    [Fact]
    public async Task Expirar_DisponeElScopeAlFinalizar()
    {
        _turnoRepoMock
            .Setup(r => r.GetPendientePagoVencidosAsync(It.IsAny<DateTime>()))
            .ReturnsAsync([]);

        await InvocarExpirarAsync();

        _scopeMock.Verify(s => s.Dispose(), Times.Once,
            "el 'using var scope' debe liberar el scope para evitar memory leaks");
    }

    [Fact]
    public async Task Expirar_CuandoExcepcion_ElScopeSigueDisposed()
    {
        _turnoRepoMock
            .Setup(r => r.GetPendientePagoVencidosAsync(It.IsAny<DateTime>()))
            .ThrowsAsync(new Exception("error"));

        await InvocarExpirarAsync(); // no lanza

        _scopeMock.Verify(s => s.Dispose(), Times.Once,
            "el 'using' garantiza Dispose() aunque haya excepción en el cuerpo");
    }
}
