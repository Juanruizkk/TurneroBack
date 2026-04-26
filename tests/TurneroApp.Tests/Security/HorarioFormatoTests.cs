using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TurneroApp.Application.DTOs.Usuario;
using TurneroApp.Application.Interfaces;
using TurneroApp.Application.Services;
using TurneroApp.Tests.Helpers;
using Xunit;

namespace TurneroApp.Tests.Security;

/// <summary>
/// Tests unitarios para el parsing de horas en HorarioLaboral (B-03).
/// Verifica que TimeSpan.TryParse reemplazó a TimeSpan.Parse, de modo que
/// un formato inválido lanza InvalidOperationException (manejable → 400)
/// y no FormatException (no manejable → 500 en producción).
/// </summary>
public class HorarioFormatoTests
{
    private readonly Mock<IUsuarioRepository>            _usuarioRepo         = new();
    private readonly Mock<IUsuarioSucursalRolRepository> _usrRepo             = new();
    private readonly Mock<ISucursalRepository>           _sucursalRepo        = new();
    private readonly Mock<IEmailService>                 _emailService        = new();
    private readonly Mock<IHorarioLaboralRepository>     _horarioRepo         = new();
    private readonly Mock<IUsuarioServicioRepository>    _usuarioServicioRepo = new();
    private readonly Mock<ITurnoRepository>              _turnoRepo           = new();
    private readonly Mock<ISuscripcionRepository>        _suscripcionRepo     = new();
    private readonly UsuarioService                       _sut;

    public HorarioFormatoTests()
    {
        _sut = new UsuarioService(
            _usuarioRepo.Object,
            _usrRepo.Object,
            _sucursalRepo.Object,
            _emailService.Object,
            "http://localhost:3000",
            _horarioRepo.Object,
            _usuarioServicioRepo.Object,
            _turnoRepo.Object,
            _suscripcionRepo.Object,
            new SuscripcionGuard(_suscripcionRepo.Object),
            Mock.Of<ILogger<UsuarioService>>());

        // El repo de horarios debe enumerar la secuencia lazy al guardar.
        // Sin este Callback, el LINQ Select no se ejecuta y la excepción no se lanza.
        _horarioRepo
            .Setup(r => r.ReplaceAsync(
                It.IsAny<Guid>(),
                It.IsAny<IEnumerable<TurneroApp.Domain.Entities.HorarioLaboral>>()))
            .Returns<Guid, IEnumerable<TurneroApp.Domain.Entities.HorarioLaboral>>(
                (usrId, horarios) =>
                {
                    horarios.ToList(); // fuerza la enumeración → dispara la excepción
                    return Task.CompletedTask;
                });
    }

    /// <summary>
    /// Test 23 — Hora con formato inválido debe lanzar InvalidOperationException
    /// con mensaje "Formato de hora inválido". No debe lanzar FormatException.
    /// </summary>
    [Theory]
    [InlineData("25:00")]    // hora fuera de rango
    [InlineData("texto")]    // string arbitrario
    [InlineData("10:99")]    // minutos inválidos
    [InlineData("")]         // vacío
    public async Task ReplaceHorarios_HoraInvalida_LanzaInvalidOperationException(
        string horaInvalida)
    {
        // Arrange
        var usr = UsuarioTestFactory.CrearUSR(UsuarioTestFactory.CrearUsuario("u@t.com", "pass"));
        _usrRepo.Setup(r => r.GetByIdAsync(usr.Id)).ReturnsAsync(usr);

        var dtos = new List<HorarioLaboralDto>
        {
            new(DiaSemana: 1, HoraDesde: horaInvalida, HoraHasta: "18:00", Activo: true)
        };

        // Act
        var act = () => _sut.ReplaceHorariosAsync(usr.Id, dtos);

        // Assert — debe ser InvalidOperationException (controlable por el controller → 400)
        //          NO debe ser FormatException (no controlada → 500 en prod)
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Formato de hora inválido*");
    }

    /// <summary>
    /// Test 23b — HoraHasta con formato inválido también lanza InvalidOperationException.
    /// </summary>
    [Fact]
    public async Task ReplaceHorarios_HoraHastaInvalida_LanzaInvalidOperationException()
    {
        var usr = UsuarioTestFactory.CrearUSR(UsuarioTestFactory.CrearUsuario("u@t.com", "pass"));
        _usrRepo.Setup(r => r.GetByIdAsync(usr.Id)).ReturnsAsync(usr);

        var dtos = new List<HorarioLaboralDto>
        {
            new(DiaSemana: 1, HoraDesde: "09:00", HoraHasta: "INVALIDA", Activo: true)
        };

        var act = () => _sut.ReplaceHorariosAsync(usr.Id, dtos);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Formato de hora inválido*");
    }

    /// <summary>
    /// Test 24 — Horas con formato válido no deben lanzar excepción.
    /// </summary>
    [Theory]
    [InlineData("09:00", "18:00")]
    [InlineData("00:00", "23:59")]
    [InlineData("08:30", "17:30")]
    public async Task ReplaceHorarios_HoraValida_NoLanzaExcepcion(
        string desde, string hasta)
    {
        // Arrange
        var usr = UsuarioTestFactory.CrearUSR(UsuarioTestFactory.CrearUsuario("u@t.com", "pass"));
        _usrRepo.Setup(r => r.GetByIdAsync(usr.Id)).ReturnsAsync(usr);
        _horarioRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        var dtos = new List<HorarioLaboralDto>
        {
            new(DiaSemana: 1, HoraDesde: desde, HoraHasta: hasta, Activo: true)
        };

        // Act
        var act = () => _sut.ReplaceHorariosAsync(usr.Id, dtos);

        // Assert
        await act.Should().NotThrowAsync(
            $"las horas {desde}–{hasta} son válidas y no deben fallar");
    }
}
