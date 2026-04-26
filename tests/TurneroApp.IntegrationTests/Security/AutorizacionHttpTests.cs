using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TurneroApp.Domain.Enums;
using TurneroApp.IntegrationTests.Helpers;
using TurneroApp.IntegrationTests.Infrastructure;
using Xunit;

namespace TurneroApp.IntegrationTests.Security;

/// <summary>
/// Tests HTTP de autorización.
/// Hallazgos cubiertos: C-02 (SuperAdminController), A-01 (endpoints sin [Authorize]),
/// A-06 (campo Origen en turnos).
/// </summary>
[Collection("HttpTests")]
public class AutorizacionHttpTests : HttpIntegrationTestBase
{
    public AutorizacionHttpTests(HttpTestFixture factory) : base(factory) { }

    // ── C-02: SuperAdminController requiere [Authorize(Policy="SuperAdmin")] ──

    /// <summary>Test 1 — SuperAdminController sin token → 401.</summary>
    [Fact]
    public async Task SuperAdmin_SinToken_Devuelve401()
    {
        SinToken();
        var response = await Client.GetAsync("/api/superadmin/sucursales");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Test 2 — SuperAdminController con token de usuario normal → 403.</summary>
    [Fact]
    public async Task SuperAdmin_ConTokenNormal_Devuelve403()
    {
        UsarToken(TokenHelper.GenerarToken(esSuperAdmin: false));
        var response = await Client.GetAsync("/api/superadmin/sucursales");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── A-01: Endpoints de staff requieren autenticación ───────────────────

    /// <summary>Test 3 — Endpoints de staff sin token → 401.</summary>
    [Theory]
    [InlineData("GET", "/api/sucursales")]
    [InlineData("GET", "/api/servicios")]
    [InlineData("GET", "/api/clientes")]
    [InlineData("GET", "/api/usuarios")]
    [InlineData("GET", "/api/turnos")]
    public async Task EndpointsDeStaff_SinToken_Devuelven401(string method, string url)
    {
        SinToken();
        var response = await Client.SendAsync(
            new HttpRequestMessage(new HttpMethod(method), url));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            $"el endpoint {method} {url} debe requerir autenticación");
    }

    /// <summary>Test 4 — Endpoints públicos sin token → nunca 401.</summary>
    [Theory]
    [InlineData("/api/turnos/by-token/TOKEN_INEXISTENTE")]
    [InlineData("/api/publico/slug-inexistente/perfil")]
    public async Task EndpointsPublicos_SinToken_NoDevuelven401(string url)
    {
        SinToken();
        var response = await Client.GetAsync(url);
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
            $"el endpoint {url} es público y no debe requerir autenticación");
    }

    // ── A-06: Origen determinado server-side ──────────────────────────────

    /// <summary>
    /// Test 7 — POST /api/turnos sin token → Origen = Web.
    /// El campo Origen se asigna en el controller según User.Identity.IsAuthenticated.
    /// </summary>
    [Fact]
    public async Task CrearTurno_SinToken_OrigenEsWeb()
    {
        // Arrange — seed completo con suscripción activa
        var (sucursalId, clienteId, servicioId, profId) =
            await SeedPrerequisitosAsync();

        SinToken();

        var response = await Client.PostAsync("/api/turnos",
            BuildTurnoContent(sucursalId, clienteId, servicioId, profId));

        // Assert — 201 o 200 y Origen = Web
        response.StatusCode.Should().BeOneOf(
            new[] { HttpStatusCode.Created, HttpStatusCode.OK },
            "el turno debe crearse sin token");

        var turno = await Db.Turnos
            .IgnoreQueryFilters()
            .SingleAsync(t => t.ProfesionalId == profId);
        turno.Origen.Should().Be(OrigenTurno.Web,
            "request sin token → Origen = Web");
    }

    /// <summary>
    /// Test 8 — POST /api/turnos con token → Origen = Administrativo.
    /// </summary>
    [Fact]
    public async Task CrearTurno_ConToken_OrigenEsAdministrativo()
    {
        // Arrange
        var (sucursalId, clienteId, servicioId, profId) =
            await SeedPrerequisitosAsync();

        UsarToken(TokenHelper.ParaSucursal(sucursalId));

        var response = await Client.PostAsync("/api/turnos",
            BuildTurnoContent(sucursalId, clienteId, servicioId, profId));

        response.StatusCode.Should().BeOneOf(
            new[] { HttpStatusCode.Created, HttpStatusCode.OK },
            "el turno debe crearse con token");

        var turno = await Db.Turnos
            .IgnoreQueryFilters()
            .SingleAsync(t => t.ProfesionalId == profId);
        turno.Origen.Should().Be(OrigenTurno.Administrativo,
            "request con token → Origen = Administrativo");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<(Guid sucursalId, Guid clienteId, Guid servicioId, Guid profId)>
        SeedPrerequisitosAsync()
    {
        var plan      = await SeedHelper.SeedPlanAsync(Db);
        var sucursal  = await SeedHelper.SeedSucursalAsync(Db);
        await SeedHelper.SeedSuscripcionAsync(Db, sucursal.Id, plan.Id);
        var cliente   = await SeedHelper.SeedClienteAsync(Db, sucursal.Id);
        var servicio  = await SeedHelper.SeedServicioAsync(Db, sucursal.Id);
        var usr       = await SeedHelper.SeedProfesionalConUSRAsync(Db, sucursal.Id);
        return (sucursal.Id, cliente.Id, servicio.Id, usr.UsuarioId);
    }

    private static StringContent BuildTurnoContent(
        Guid sucursalId, Guid clienteId, Guid servicioId, Guid profId)
    {
        var dto = new
        {
            sucursalId,
            clienteId,
            servicioId,
            profesionalId   = profId,
            fechaHoraInicio = DateTime.UtcNow.AddDays(2).Date.AddHours(10),
        };
        return new StringContent(
            JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");
    }
}
