using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Xunit;
using TurneroApp.Api.Hubs;
using TurneroApp.Api.Services;
using TurneroApp.Application.DTOs.Turno;
using TurneroApp.Domain.Enums;

namespace TurneroApp.Tests.Services;

/// <summary>
/// Verifica que <see cref="TurnoNotificacionService"/> enrute los mensajes SignalR
/// a los grupos correctos del hub.
///
/// Grupos utilizados:
/// • <c>profesional-{ProfesionalId}</c> — recibe <c>NuevoTurno</c> y <c>TurnoCancelado</c>.
/// • <c>admins-{SucursalId}</c>         — recibe <c>NuevoTurno</c> y <c>TurnoCancelado</c>.
///
/// El test mockea la cadena <c>IHubContext → IHubClients → IClientProxy</c>
/// para inspeccionar qué grupos y qué métodos se invocan sin necesitar
/// un servidor SignalR real.
/// </summary>
public class TurnoNotificacionServiceTests
{
    // ── Mocks ─────────────────────────────────────────────────────────────────

    private readonly Mock<IHubContext<TurneroHub>> _hubContext = new();
    private readonly Mock<IHubClients>             _hubClients = new();
    private readonly Mock<IClientProxy>            _clientProxy = new();
    private readonly TurnoNotificacionService      _sut;

    public TurnoNotificacionServiceTests()
    {
        // Cadena de mocks: HubContext.Clients → IHubClients.Group → IClientProxy
        _hubContext.Setup(h => h.Clients).Returns(_hubClients.Object);
        _hubClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_clientProxy.Object);
        _clientProxy
            .Setup(p => p.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new TurnoNotificacionService(_hubContext.Object);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static TurnoResponseDto CrearDto(EstadoTurno estado = EstadoTurno.Confirmado)
    {
        var profesionalId = Guid.NewGuid();
        var sucursalId    = Guid.NewGuid();
        return new TurnoResponseDto
        {
            Id               = Guid.NewGuid(),
            SucursalId       = sucursalId,
            ProfesionalId    = profesionalId,
            ClienteId        = Guid.NewGuid(),
            ClienteNombre    = "Ana López",
            ServicioId       = Guid.NewGuid(),
            ServicioNombre   = "Corte",
            FechaHoraInicio  = DateTime.Now.AddDays(1),
            FechaHoraFin     = DateTime.Now.AddDays(1).AddMinutes(30),
            Estado           = estado,
            Origen           = OrigenTurno.Web,
            TokenGestion     = Guid.NewGuid().ToString("N"),
            PrecioServicio   = 5000m,
            TotalCobrado     = 0m,
        };
    }

    // =========================================================================
    // ── NotificarNuevoTurnoAsync ───────────────────────────────────────────────
    // =========================================================================

    [Fact]
    public async Task NotificarNuevoTurno_EnviaAlGrupoProfesional()
    {
        var dto = CrearDto();

        await _sut.NotificarNuevoTurnoAsync(dto);

        _hubClients.Verify(
            c => c.Group($"profesional-{dto.ProfesionalId}"),
            Times.Once,
            "debe dirigir el mensaje al grupo del profesional asignado al turno");
    }

    [Fact]
    public async Task NotificarNuevoTurno_EnviaAlGrupoAdmins()
    {
        var dto = CrearDto();

        await _sut.NotificarNuevoTurnoAsync(dto);

        _hubClients.Verify(
            c => c.Group($"admins-{dto.SucursalId}"),
            Times.Once,
            "debe dirigir el mensaje al grupo de admins/cajeros de la sucursal");
    }

    [Fact]
    public async Task NotificarNuevoTurno_UsaMetodoNuevoTurno()
    {
        var dto = CrearDto();

        await _sut.NotificarNuevoTurnoAsync(dto);

        // SendAsync es una extensión que llama a SendCoreAsync internamente
        _clientProxy.Verify(
            p => p.SendCoreAsync(
                "NuevoTurno",
                It.Is<object?[]>(args => args.Length == 1),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2),
            "debe llamar SendAsync('NuevoTurno', ...) dos veces: una por el grupo " +
            "del profesional y otra por el grupo de admins");
    }

    // =========================================================================
    // ── NotificarCancelacionAsync ──────────────────────────────────────────────
    // =========================================================================

    [Fact]
    public async Task NotificarCancelacion_EnviaAlGrupoProfesional()
    {
        var dto = CrearDto(EstadoTurno.CanceladoStaff);

        await _sut.NotificarCancelacionAsync(dto);

        _hubClients.Verify(
            c => c.Group($"profesional-{dto.ProfesionalId}"),
            Times.Once,
            "la cancelación debe notificarse al profesional para que actualice su agenda");
    }

    [Fact]
    public async Task NotificarCancelacion_EnviaAlGrupoAdmins()
    {
        var dto = CrearDto(EstadoTurno.CanceladoCliente);

        await _sut.NotificarCancelacionAsync(dto);

        _hubClients.Verify(
            c => c.Group($"admins-{dto.SucursalId}"),
            Times.Once,
            "la cancelación debe notificarse a los admins para que vean el slot liberado");
    }

    [Fact]
    public async Task NotificarCancelacion_UsaMetodoTurnoCancelado()
    {
        var dto = CrearDto(EstadoTurno.CanceladoCliente);

        await _sut.NotificarCancelacionAsync(dto);

        _clientProxy.Verify(
            p => p.SendCoreAsync(
                "TurnoCancelado",
                It.Is<object?[]>(args => args.Length == 1),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2),
            "debe llamar SendAsync('TurnoCancelado', ...) dos veces: una por el grupo " +
            "del profesional y otra por el grupo de admins");
    }
}
