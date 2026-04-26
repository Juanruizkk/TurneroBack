using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using TurneroApp.Application.DTOs.Cliente;
using TurneroApp.Application.DTOs.Servicio;
using TurneroApp.Application.DTOs.Turno;
using Xunit;

namespace TurneroApp.Tests.Security;

/// <summary>
/// Tests unitarios para las validaciones de DataAnnotations en DTOs (M-02).
/// Verifica que los atributos [Required], [Range], [MaxLength] estén presentes
/// y funcionen correctamente, previniendo requests con datos inválidos.
/// </summary>
public class DtoValidacionesTests
{
    // ── CreateTurnoDto ──────────────────────────────────────────────────────

    /// <summary>
    /// Test 28 — Observaciones que excede MaxLength(500) → falla validación.
    /// Nota: [Required] en Guid/DateTime no es validable via DataAnnotations
    /// para tipos de valor no-nulos — esa validación ocurre en el model binding.
    /// </summary>
    [Fact]
    public void CreateTurnoDto_ObservacionesMuyLargas_FallaValidacion()
    {
        var dto = new CreateTurnoDto
        {
            SucursalId      = Guid.NewGuid(),
            ClienteId       = Guid.NewGuid(),
            ServicioId      = Guid.NewGuid(),
            ProfesionalId   = Guid.NewGuid(),
            FechaHoraInicio = DateTime.UtcNow.AddDays(1),
            Observaciones   = new string('x', 501), // supera MaxLength(500)
        };

        var resultados = Validar(dto);

        resultados.Should().Contain(r => r.MemberNames.Contains("Observaciones"),
            "Observaciones tiene [MaxLength(500)] y debe fallar con 501 caracteres");
    }

    /// <summary>Test complementario — Observaciones dentro del límite → pasa.</summary>
    [Fact]
    public void CreateTurnoDto_ObservacionesDentroLimite_PasaValidacion()
    {
        var dto = new CreateTurnoDto
        {
            SucursalId      = Guid.NewGuid(),
            ClienteId       = Guid.NewGuid(),
            ServicioId      = Guid.NewGuid(),
            ProfesionalId   = Guid.NewGuid(),
            FechaHoraInicio = DateTime.UtcNow.AddDays(1),
            Observaciones   = new string('x', 500),
        };

        var resultados = Validar(dto);

        resultados.Should().NotContain(r => r.MemberNames.Contains("Observaciones"));
    }

    // ── CreateClienteDto ────────────────────────────────────────────────────

    /// <summary>Test 29 — Nombre vacío en CreateClienteDto → falla [Required].</summary>
    [Fact]
    public void CreateClienteDto_NombreVacio_FallaValidacion()
    {
        var dto = new CreateClienteDto
        {
            SucursalId = Guid.NewGuid(),
            Nombre     = "",                   // vacío → falla [Required]
            Apellido   = "Test",
        };

        var resultados = Validar(dto);

        resultados.Should().Contain(r => r.MemberNames.Contains("Nombre"),
            "Nombre es [Required] y no puede estar vacío");
    }

    /// <summary>Test complementario — Apellido vacío → falla [Required].</summary>
    [Fact]
    public void CreateClienteDto_ApellidoVacio_FallaValidacion()
    {
        var dto = new CreateClienteDto
        {
            SucursalId = Guid.NewGuid(),
            Nombre     = "Juan",
            Apellido   = "",               // vacío → falla [Required]
        };

        var resultados = Validar(dto);

        resultados.Should().Contain(r => r.MemberNames.Contains("Apellido"));
    }

    // ── CreateServicioDto ───────────────────────────────────────────────────

    /// <summary>Test 30 — Precio negativo en CreateServicioDto → falla [Range(0, ...)].</summary>
    [Fact]
    public void CreateServicioDto_PrecioNegativo_FallaValidacion()
    {
        var dto = new CreateServicioDto
        {
            SucursalId         = Guid.NewGuid(),
            Nombre             = "Corte",
            Precio             = -1,           // falla [Range(0, 9999999.99)]
            DuracionClienteMin = 30,
            DuracionAgendaMin  = 30,
        };

        var resultados = Validar(dto);

        resultados.Should().Contain(r => r.MemberNames.Contains("Precio"),
            "Precio no puede ser negativo");
    }

    /// <summary>Test 31 — DuracionClienteMin = 0 → falla [Range(1, 1440)].</summary>
    [Fact]
    public void CreateServicioDto_DuracionCero_FallaValidacion()
    {
        var dto = new CreateServicioDto
        {
            SucursalId         = Guid.NewGuid(),
            Nombre             = "Corte",
            Precio             = 50,
            DuracionClienteMin = 0,            // falla [Range(1, 1440)]
            DuracionAgendaMin  = 30,
        };

        var resultados = Validar(dto);

        resultados.Should().Contain(r => r.MemberNames.Contains("DuracionClienteMin"),
            "la duración mínima es 1 minuto");
    }

    /// <summary>Test 31b — DuracionAgendaMin = 0 → falla [Range(1, 1440)].</summary>
    [Fact]
    public void CreateServicioDto_DuracionAgendaCero_FallaValidacion()
    {
        var dto = new CreateServicioDto
        {
            SucursalId         = Guid.NewGuid(),
            Nombre             = "Corte",
            Precio             = 50,
            DuracionClienteMin = 30,
            DuracionAgendaMin  = 0,            // falla [Range(1, 1440)]
        };

        var resultados = Validar(dto);

        resultados.Should().Contain(r => r.MemberNames.Contains("DuracionAgendaMin"));
    }

    // ── Helper ──────────────────────────────────────────────────────────────

    private static List<ValidationResult> Validar(object dto)
    {
        var context    = new ValidationContext(dto);
        var resultados = new List<ValidationResult>();
        Validator.TryValidateObject(dto, context, resultados, validateAllProperties: true);
        return resultados;
    }
}
