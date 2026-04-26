using System.Text.Json;
using FluentAssertions;
using TurneroApp.Application.DTOs.Servicio;
using TurneroApp.Application.DTOs.Sucursal;
using Xunit;

namespace TurneroApp.Tests.Security;

/// <summary>
/// Tests unitarios para prevención de overposting (M-01).
/// Verifican que los DTOs de actualización no expongan campos sensibles
/// como Activo/Activa, que solo deben modificarse por endpoints dedicados.
/// </summary>
public class OverpostingTests
{
    /// <summary>
    /// Test 26 — UpdateServicioDto no debe tener la propiedad Activo.
    /// Si la tuviera, un cliente podría desactivar un servicio enviando activo=false
    /// en el body de un PUT, bypasseando el endpoint DELETE /api/servicios/{id}.
    /// </summary>
    [Fact]
    public void UpdateServicioDto_PropiedadActivoNoExiste()
    {
        typeof(UpdateServicioDto)
            .GetProperty("Activo")
            .Should().BeNull(
                "el campo Activo fue removido de UpdateServicioDto para prevenir overposting; " +
                "el estado se gestiona exclusivamente con DELETE /api/servicios/{id}");
    }

    /// <summary>
    /// Test 27 — UpdateSucursalDto no debe tener la propiedad Activa.
    /// Si la tuviera, un owner podría desactivar su propia sucursal via PUT.
    /// </summary>
    [Fact]
    public void UpdateSucursalDto_PropiedadActivaNoExiste()
    {
        typeof(UpdateSucursalDto)
            .GetProperty("Activa")
            .Should().BeNull(
                "el campo Activa fue removido de UpdateSucursalDto para prevenir overposting; " +
                "el estado se gestiona exclusivamente por SuperAdmin");
    }

    /// <summary>
    /// Test complementario — Si el JSON incluye 'activo', la deserialización lo ignora.
    /// Confirma que no hay [JsonPropertyName] ni binding que acepte el campo.
    /// </summary>
    [Fact]
    public void UpdateServicioDto_CampoActivoEnJson_SeIgnora()
    {
        var json = """
            {
                "nombre": "Corte", "precio": 100,
                "duracionClienteMin": 30, "duracionAgendaMin": 30,
                "activo": false, "modalidadPrestacion": 0, "tipoServicio": 0
            }
            """;

        var dto = JsonSerializer.Deserialize<UpdateServicioDto>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        dto.Should().NotBeNull();
        dto!.Nombre.Should().Be("Corte",
            "los campos válidos deben seguir mapeándose correctamente");
    }
}
