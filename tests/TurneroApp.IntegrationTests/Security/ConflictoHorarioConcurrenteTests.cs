using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TurneroApp.IntegrationTests.Helpers;
using TurneroApp.IntegrationTests.Infrastructure;
using Xunit;

namespace TurneroApp.IntegrationTests.Security;

/// <summary>
/// Test HTTP de race condition (M-03).
/// Verifica que la transacción Serializable en TurnoService.CreateAsync
/// previene que dos requests simultáneos creen el mismo turno (TOCTOU).
/// </summary>
[Collection("HttpTests")]
public class ConflictoHorarioConcurrenteTests : HttpIntegrationTestBase
{
    public ConflictoHorarioConcurrenteTests(HttpTestFixture factory) : base(factory) { }

    /// <summary>
    /// Test 11 — 5 requests simultáneos para el mismo slot de profesional.
    /// Solo 1 debe persistirse (201 Created); el resto deben recibir 409 Conflict.
    /// La transacción Serializable de PostgreSQL detecta el conflicto y el
    /// servicio lo traduce en InvalidOperationException → 409.
    ///
    /// Por qué HTTP y no servicio directo: con Transaction Serializable, cada
    /// request necesita su propia conexión de BD. Task.WhenAll sobre HttpClient
    /// replica exactamente ese escenario de producción.
    /// </summary>
    [Fact]
    public async Task CrearTurno_ConcurrenteMismoHorario_SoloUnoSePersiste()
    {
        // Arrange
        var plan     = await SeedHelper.SeedPlanAsync(Db);
        var sucursal = await SeedHelper.SeedSucursalAsync(Db);
        await SeedHelper.SeedSuscripcionAsync(Db, sucursal.Id, plan.Id);
        var cliente  = await SeedHelper.SeedClienteAsync(Db, sucursal.Id);
        var servicio = await SeedHelper.SeedServicioAsync(Db, sucursal.Id);
        var usr      = await SeedHelper.SeedProfesionalConUSRAsync(Db, sucursal.Id);

        var inicio = DateTime.UtcNow.AddDays(5).Date.AddHours(10); // slot fijo

        var payload = JsonSerializer.Serialize(new
        {
            sucursalId      = sucursal.Id,
            clienteId       = cliente.Id,
            servicioId      = servicio.Id,
            profesionalId   = usr.UsuarioId,
            fechaHoraInicio = inicio,
        });

        // Act — 5 clientes HTTP independientes, cada uno con su propio DbContext
        var tasks = Enumerable.Range(0, 5).Select(_ =>
        {
            var client  = Factory.CreateClient();
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            return client.PostAsync("/api/turnos", content);
        });

        var responses = await Task.WhenAll(tasks);

        // Assert
        var creados    = responses.Count(r => r.StatusCode == HttpStatusCode.Created
                                           || r.StatusCode == HttpStatusCode.OK);
        var conflictos = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        creados.Should().Be(1,
            "la transacción Serializable solo permite que un request gane la carrera");
        conflictos.Should().Be(4,
            "los otros 4 requests deben recibir 409 Conflict");

        // Y en la DB: exactamente 1 turno para ese slot
        var turnosEnDb = await Db.Turnos
            .IgnoreQueryFilters()
            .CountAsync(t => t.ProfesionalId == usr.UsuarioId
                          && t.FechaHoraInicio == inicio);
        turnosEnDb.Should().Be(1,
            "solo debe haber un turno persistido en la base de datos");
    }
}
