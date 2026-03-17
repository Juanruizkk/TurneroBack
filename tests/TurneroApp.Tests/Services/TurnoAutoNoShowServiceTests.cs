using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using TurneroApp.Api.Services;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;
using TurneroApp.Tests.Helpers;

namespace TurneroApp.Tests.Services;

/// <summary>
/// Suite de tests unitarios para <see cref="TurnoAutoNoShowService"/>.
///
/// NOTA DE DISEÑO: el método <c>EjecutarAsync</c> se declara <c>internal</c>
/// en producción (en lugar de <c>private</c>) para permitir su invocación directa
/// desde los tests, evitando depender del bucle de <c>ExecuteAsync</c> que puede
/// esperar hasta 24 h antes de la primera ejecución.
/// La visibilidad se habilita con <c>[assembly: InternalsVisibleTo("TurneroApp.Tests")]</c>
/// configurado en <c>TurneroApp.Api.csproj</c>.
/// </summary>
public class TurnoAutoNoShowServiceTests
{
    // ── Mocks ─────────────────────────────────────────────────────────────────

    private readonly Mock<IServiceScopeFactory>            _scopeFactoryMock;
    private readonly Mock<IServiceScope>                   _scopeMock;
    private readonly Mock<IServiceProvider>                _serviceProviderMock;
    private readonly Mock<ITurnoRepository>                _turnoRepoMock;
    private readonly Mock<ILogger<TurnoAutoNoShowService>> _loggerMock;

    private readonly TurnoAutoNoShowService _sut;

    // ── Constructor (setup compartido) ────────────────────────────────────────

    public TurnoAutoNoShowServiceTests()
    {
        _scopeFactoryMock    = new Mock<IServiceScopeFactory>();
        _scopeMock           = new Mock<IServiceScope>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _turnoRepoMock       = new Mock<ITurnoRepository>();
        _loggerMock          = new Mock<ILogger<TurnoAutoNoShowService>>();

        // GetRequiredService<T>() es una extension method que internamente llama
        // IServiceProvider.GetService(typeof(T)) y lanza si es null.
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(ITurnoRepository)))
            .Returns(_turnoRepoMock.Object);

        _scopeMock
            .Setup(s => s.ServiceProvider)
            .Returns(_serviceProviderMock.Object);

        _scopeFactoryMock
            .Setup(f => f.CreateScope())
            .Returns(_scopeMock.Object);

        _sut = new TurnoAutoNoShowService(_scopeFactoryMock.Object, _loggerMock.Object);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    /// <summary>Invoca el método de negocio directamente, sin pasar por el bucle del BackgroundService.</summary>
    private Task InvocarEjecutarAsync()
        => _sut.EjecutarAsync(CancellationToken.None);

    // =========================================================================
    // ── Tests ─────────────────────────────────────────────────────────────────
    // =========================================================================

    // ── Sin turnos pendientes ─────────────────────────────────────────────────

    [Fact]
    public async Task Ejecutar_SinTurnosPendientes_NoLlamaAUpdateRange()
    {
        // Arrange
        _turnoRepoMock
            .Setup(r => r.GetConfirmadosPendientesAntesDeAsync(It.IsAny<DateTime>()))
            .ReturnsAsync([]);

        // Act
        await InvocarEjecutarAsync();

        // Assert
        _turnoRepoMock.Verify(
            r => r.UpdateRange(It.IsAny<IEnumerable<Turno>>()),
            Times.Never,
            "no debe llamar a UpdateRange si no hay turnos que marcar");
    }

    [Fact]
    public async Task Ejecutar_SinTurnosPendientes_NoLlamaASaveChanges()
    {
        // Arrange
        _turnoRepoMock
            .Setup(r => r.GetConfirmadosPendientesAntesDeAsync(It.IsAny<DateTime>()))
            .ReturnsAsync([]);

        // Act
        await InvocarEjecutarAsync();

        // Assert
        _turnoRepoMock.Verify(
            r => r.SaveChangesAsync(),
            Times.Never,
            "no debe persistir cambios si no hay turnos que marcar");
    }

    // ── Con turnos pendientes — estado ────────────────────────────────────────

    [Fact]
    public async Task Ejecutar_ConTurnosPendientes_ActualizaEstadoANoShow()
    {
        // Arrange
        var turno1 = TurnoTestFactory.CrearTurnoConRelaciones();
        var turno2 = TurnoTestFactory.CrearTurnoConRelaciones();
        turno1.Estado = EstadoTurno.Confirmado;
        turno2.Estado = EstadoTurno.Pendiente;

        _turnoRepoMock
            .Setup(r => r.GetConfirmadosPendientesAntesDeAsync(It.IsAny<DateTime>()))
            .ReturnsAsync([turno1, turno2]);

        // Act
        await InvocarEjecutarAsync();

        // Assert
        turno1.Estado.Should().Be(EstadoTurno.NoShow,
            "un turno Confirmado sin asistencia debe pasar a NoShow");
        turno2.Estado.Should().Be(EstadoTurno.NoShow,
            "un turno Pendiente sin asistencia debe pasar a NoShow");
    }

    // ── Con turnos pendientes — llamadas al repositorio ───────────────────────

    [Fact]
    public async Task Ejecutar_ConTurnosPendientes_LlamaUpdateRangeUnaVez()
    {
        // Arrange
        var turnos = new[]
        {
            TurnoTestFactory.CrearTurnoConRelaciones(),
            TurnoTestFactory.CrearTurnoConRelaciones(),
            TurnoTestFactory.CrearTurnoConRelaciones(),
        };

        _turnoRepoMock
            .Setup(r => r.GetConfirmadosPendientesAntesDeAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(turnos);

        // Act
        await InvocarEjecutarAsync();

        // Assert
        _turnoRepoMock.Verify(
            r => r.UpdateRange(It.IsAny<IEnumerable<Turno>>()),
            Times.Once,
            "debe llamar a UpdateRange exactamente una vez con todos los turnos");
    }

    [Fact]
    public async Task Ejecutar_ConTurnosPendientes_LlamaSaveChangesUnaVez()
    {
        // Arrange
        _turnoRepoMock
            .Setup(r => r.GetConfirmadosPendientesAntesDeAsync(It.IsAny<DateTime>()))
            .ReturnsAsync([TurnoTestFactory.CrearTurnoConRelaciones()]);

        // Act
        await InvocarEjecutarAsync();

        // Assert
        _turnoRepoMock.Verify(
            r => r.SaveChangesAsync(),
            Times.Once,
            "debe persistir todos los cambios en una sola llamada");
    }

    [Fact]
    public async Task Ejecutar_ConTurnosPendientes_UpdateRangeRecibeTodosLosTurnos()
    {
        // Arrange
        var turno1 = TurnoTestFactory.CrearTurnoConRelaciones();
        var turno2 = TurnoTestFactory.CrearTurnoConRelaciones();
        IEnumerable<Turno>? turnosCapturados = null;

        _turnoRepoMock
            .Setup(r => r.GetConfirmadosPendientesAntesDeAsync(It.IsAny<DateTime>()))
            .ReturnsAsync([turno1, turno2]);

        _turnoRepoMock
            .Setup(r => r.UpdateRange(It.IsAny<IEnumerable<Turno>>()))
            .Callback<IEnumerable<Turno>>(list => turnosCapturados = list);

        // Act
        await InvocarEjecutarAsync();

        // Assert
        turnosCapturados.Should().NotBeNull();
        turnosCapturados!.Should().HaveCount(2)
            .And.Contain(turno1)
            .And.Contain(turno2);
    }

    // ── Fecha límite ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Ejecutar_FechaLimite_EsMedianocheDeDiaActual()
    {
        // Arrange
        DateTime? fechaCapturada = null;

        _turnoRepoMock
            .Setup(r => r.GetConfirmadosPendientesAntesDeAsync(It.IsAny<DateTime>()))
            .Callback<DateTime>(f => fechaCapturada = f)
            .ReturnsAsync([]);

        // Act
        await InvocarEjecutarAsync();

        // Assert
        fechaCapturada.Should().NotBeNull();
        fechaCapturada!.Value.Date.Should().Be(DateTime.Today,
            "la fecha límite debe corresponder al día actual");
        fechaCapturada.Value.TimeOfDay.Should().Be(TimeSpan.Zero,
            "la fecha límite debe ser las 00:00:00 (medianoche) para excluir los de hoy");
    }

    // ── Manejo de errores ─────────────────────────────────────────────────────

    [Fact]
    public async Task Ejecutar_CuandoRepoLanzaExcepcion_NoPropagarElError()
    {
        // Arrange
        _turnoRepoMock
            .Setup(r => r.GetConfirmadosPendientesAntesDeAsync(It.IsAny<DateTime>()))
            .ThrowsAsync(new InvalidOperationException("Fallo de base de datos simulado"));

        // Act
        var act = () => InvocarEjecutarAsync();

        // Assert
        await act.Should().NotThrowAsync(
            "el servicio debe capturar la excepción internamente y no derruir el host");
    }

    [Fact]
    public async Task Ejecutar_CuandoSaveChangesLanzaExcepcion_NoPropagarElError()
    {
        // Arrange
        _turnoRepoMock
            .Setup(r => r.GetConfirmadosPendientesAntesDeAsync(It.IsAny<DateTime>()))
            .ReturnsAsync([TurnoTestFactory.CrearTurnoConRelaciones()]);

        _turnoRepoMock
            .Setup(r => r.SaveChangesAsync())
            .ThrowsAsync(new Exception("Timeout de DB simulado"));

        // Act
        var act = () => InvocarEjecutarAsync();

        // Assert
        await act.Should().NotThrowAsync(
            "cualquier error durante la persistencia debe ser absorbido por el try/catch");
    }

    // ── Ciclo de vida del scope ───────────────────────────────────────────────

    [Fact]
    public async Task Ejecutar_SiempreCreaExactamenteUnScope()
    {
        // Arrange
        _turnoRepoMock
            .Setup(r => r.GetConfirmadosPendientesAntesDeAsync(It.IsAny<DateTime>()))
            .ReturnsAsync([]);

        // Act
        await InvocarEjecutarAsync();

        // Assert
        _scopeFactoryMock.Verify(f => f.CreateScope(), Times.Once,
            "cada ejecución debe crear un scope DI independiente");
    }

    [Fact]
    public async Task Ejecutar_DisposeDelScopeSeLlamaAlFinalizar()
    {
        // Arrange
        _turnoRepoMock
            .Setup(r => r.GetConfirmadosPendientesAntesDeAsync(It.IsAny<DateTime>()))
            .ReturnsAsync([]);

        // Act
        await InvocarEjecutarAsync();

        // Assert — 'using var scope' garantiza Dispose() incluso si hay error
        _scopeMock.Verify(s => s.Dispose(), Times.Once,
            "el scope debe liberarse al finalizar para evitar memory leaks");
    }

    [Fact]
    public async Task Ejecutar_CuandoExcepcion_ElScopeSigueDisposed()
    {
        // Arrange
        _turnoRepoMock
            .Setup(r => r.GetConfirmadosPendientesAntesDeAsync(It.IsAny<DateTime>()))
            .ThrowsAsync(new Exception("error"));

        // Act
        await InvocarEjecutarAsync(); // no lanza

        // Assert
        _scopeMock.Verify(s => s.Dispose(), Times.Once,
            "el 'using' garantiza Dispose() aunque el cuerpo del scope lance una excepción");
    }
}
