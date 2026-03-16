using FluentAssertions;
using Moq;
using Xunit;
using TurneroApp.Application.DTOs.Turno;
using TurneroApp.Application.Interfaces;
using TurneroApp.Application.Services;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;
using TurneroApp.Tests.Helpers;

namespace TurneroApp.Tests.Services;

public class TurnoServiceUpdateTests
{
    private readonly Mock<ITurnoRepository>           _turnoRepo      = new();
    private readonly Mock<IServicioRepository>        _servicioRepo   = new();
    private readonly Mock<IClienteRepository>         _clienteRepo    = new();
    private readonly Mock<ISucursalRepository>        _sucursalRepo   = new();
    private readonly Mock<IEmailService>              _emailService   = new();
    private readonly Mock<IComentarioTurnoRepository> _comentarioRepo = new();
    private readonly TurnoService                     _sut;

    public TurnoServiceUpdateTests()
    {
        _sut = new TurnoService(
            _turnoRepo.Object,
            _servicioRepo.Object,
            _clienteRepo.Object,
            _sucursalRepo.Object,
            _emailService.Object,
            _comentarioRepo.Object,
            "http://localhost:3000");
    }

    // ── Setup helpers ──────────────────────────────────────────────────────────

    private void SetupSaveOk()
    {
        _turnoRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);
    }

    private void SetupSucursalEmail(Guid sucursalId, string email = "sucursal@test.com")
    {
        _sucursalRepo.Setup(r => r.GetByIdAsync(sucursalId))
            .ReturnsAsync(new Sucursal { Id = sucursalId, Nombre = "Sucursal Test", Email = email });
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_TurnoNoEncontrado_RetornaNull()
    {
        _turnoRepo.Setup(r => r.GetByIdConRelacionesAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Turno?)null);

        var result = await _sut.UpdateEstadoAsync(Guid.NewGuid(), new UpdateTurnoDto());

        result.Should().BeNull();
    }

    [Fact]
    public async Task Update_CambiaEstado_Correctamente()
    {
        var turno = TurnoTestFactory.CrearTurnoConRelaciones();
        var dto   = new UpdateTurnoDto { Estado = EstadoTurno.Completado };

        _turnoRepo.Setup(r => r.GetByIdConRelacionesAsync(turno.Id)).ReturnsAsync(turno);
        SetupSaveOk();

        var result = await _sut.UpdateEstadoAsync(turno.Id, dto);

        result!.Estado.Should().Be(EstadoTurno.Completado);
    }

    [Fact]
    public async Task Update_CanceladoStaff_EnviaEmailAlCliente()
    {
        var cliente = TurnoTestFactory.CrearCliente("cliente@test.com");
        var turno   = TurnoTestFactory.CrearTurnoConRelaciones(cliente: cliente);
        var dto     = new UpdateTurnoDto { Estado = EstadoTurno.CanceladoStaff };

        _turnoRepo.Setup(r => r.GetByIdConRelacionesAsync(turno.Id)).ReturnsAsync(turno);
        SetupSaveOk();
        SetupSucursalEmail(turno.SucursalId);
        _emailService.Setup(e => e.EnviarCancelacionStaffAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        await _sut.UpdateEstadoAsync(turno.Id, dto);

        _emailService.Verify(e => e.EnviarCancelacionStaffAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<DateTime>()),
            Times.Once);
    }

    [Fact]
    public async Task Update_CanceladoCliente_EnviaEmailAlClienteYAlProfesional()
    {
        var cliente = TurnoTestFactory.CrearCliente("cliente@test.com");
        var turno   = TurnoTestFactory.CrearTurnoConRelaciones(cliente: cliente);
        var dto     = new UpdateTurnoDto { Estado = EstadoTurno.CanceladoCliente };

        _turnoRepo.Setup(r => r.GetByIdConRelacionesAsync(turno.Id)).ReturnsAsync(turno);
        SetupSaveOk();
        // Sucursal con email (fallback para alertaEmail cuando no hay profesional)
        SetupSucursalEmail(turno.SucursalId, "sucursal@test.com");
        _emailService.Setup(e => e.EnviarCancelacionClienteAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);
        _emailService.Setup(e => e.EnviarAlertaLiberacionAgendaAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        await _sut.UpdateEstadoAsync(turno.Id, dto);

        _emailService.Verify(e => e.EnviarCancelacionClienteAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<DateTime>()),
            Times.Once);
        _emailService.Verify(e => e.EnviarAlertaLiberacionAgendaAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()),
            Times.Once);
    }

    [Fact]
    public async Task Update_EmailCancelacion_Falla_NoLanzaException()
    {
        var cliente = TurnoTestFactory.CrearCliente("cliente@test.com");
        var turno   = TurnoTestFactory.CrearTurnoConRelaciones(cliente: cliente);
        var dto     = new UpdateTurnoDto { Estado = EstadoTurno.CanceladoStaff };

        _turnoRepo.Setup(r => r.GetByIdConRelacionesAsync(turno.Id)).ReturnsAsync(turno);
        SetupSaveOk();
        SetupSucursalEmail(turno.SucursalId);
        _emailService.Setup(e => e.EnviarCancelacionStaffAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<DateTime>()))
            .ThrowsAsync(new Exception("SMTP error"));

        await _sut.Invoking(s => s.UpdateEstadoAsync(turno.Id, dto))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task Update_Reprogramar_RecalculaFechaHoraFin()
    {
        var servicio        = TurnoTestFactory.CrearServicio(duracionAgendaMin: 60);
        var turno           = TurnoTestFactory.CrearTurnoConRelaciones(servicio: servicio);
        var nuevaFechaInicio = new DateTime(2026, 7, 1, 9, 0, 0, DateTimeKind.Utc);
        var dto = new UpdateTurnoDto
        {
            Estado          = EstadoTurno.Confirmado,
            FechaHoraInicio = nuevaFechaInicio,
            ProfesionalId   = turno.ProfesionalId
        };

        _turnoRepo.Setup(r => r.GetByIdConRelacionesAsync(turno.Id)).ReturnsAsync(turno);
        _servicioRepo.Setup(r => r.GetByIdAsync(turno.ServicioId)).ReturnsAsync(servicio);
        _turnoRepo.Setup(r => r.ExisteConflictoHorarioAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), turno.Id))
            .ReturnsAsync(false);
        SetupSaveOk();

        var result = await _sut.UpdateEstadoAsync(turno.Id, dto);

        result!.FechaHoraFin.Should().Be(nuevaFechaInicio.AddMinutes(60));
    }

    [Fact]
    public async Task Update_Reprogramar_ConConflicto_LanzaException()
    {
        var servicio        = TurnoTestFactory.CrearServicio(duracionAgendaMin: 30);
        var turno           = TurnoTestFactory.CrearTurnoConRelaciones(servicio: servicio);
        var nuevaFechaInicio = DateTime.UtcNow.AddDays(2);
        var dto = new UpdateTurnoDto
        {
            Estado          = EstadoTurno.Confirmado,
            FechaHoraInicio = nuevaFechaInicio,
            ProfesionalId   = turno.ProfesionalId
        };

        _turnoRepo.Setup(r => r.GetByIdConRelacionesAsync(turno.Id)).ReturnsAsync(turno);
        _servicioRepo.Setup(r => r.GetByIdAsync(turno.ServicioId)).ReturnsAsync(servicio);
        _turnoRepo.Setup(r => r.ExisteConflictoHorarioAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), turno.Id))
            .ReturnsAsync(true);

        await _sut.Invoking(s => s.UpdateEstadoAsync(turno.Id, dto))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Update_SoloProfesional_SinNuevaFecha_CambiaProf()
    {
        var turno             = TurnoTestFactory.CrearTurnoConRelaciones();
        var nuevoProfesionalId = Guid.NewGuid();
        var dto = new UpdateTurnoDto
        {
            Estado        = EstadoTurno.Confirmado,
            ProfesionalId = nuevoProfesionalId
        };

        _turnoRepo.Setup(r => r.GetByIdConRelacionesAsync(turno.Id)).ReturnsAsync(turno);
        SetupSaveOk();

        var result = await _sut.UpdateEstadoAsync(turno.Id, dto);

        result!.ProfesionalId.Should().Be(nuevoProfesionalId);
    }
}
