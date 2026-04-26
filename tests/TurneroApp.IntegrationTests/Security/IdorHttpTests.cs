using System.Net;
using FluentAssertions;
using TurneroApp.IntegrationTests.Helpers;
using TurneroApp.IntegrationTests.Infrastructure;
using Xunit;

namespace TurneroApp.IntegrationTests.Security;

/// <summary>
/// Tests HTTP de IDOR (Insecure Direct Object Reference).
/// Hallazgo cubierto: A-02 — acceso cruzado entre sucursales.
/// </summary>
[Collection("HttpTests")]
public class IdorHttpTests : HttpIntegrationTestBase
{
    public IdorHttpTests(HttpTestFixture factory) : base(factory) { }

    /// <summary>
    /// Test 5 — Token de sucursal A intentando acceder a ajustes de sucursal B → 403.
    /// AjustesController.ValidarAccesoSucursal compara el sucursalId del claim
    /// con el sucursalId del request; si difieren, devuelve Forbid().
    /// </summary>
    [Fact]
    public async Task Ajustes_TokenSucursalA_AccedeSucursalB_Devuelve403()
    {
        // Arrange — seed dos sucursales
        var sucursalA = await SeedHelper.SeedSucursalAsync(Db);
        var sucursalB = await SeedHelper.SeedSucursalAsync(Db);

        // Token firmado para sucursal A
        UsarToken(TokenHelper.ParaSucursal(sucursalA.Id));

        // Act — intentar acceder a los ajustes de sucursal B
        var response = await Client.GetAsync($"/api/ajustes?sucursalId={sucursalB.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "un usuario de sucursal A no puede acceder a datos de sucursal B (IDOR)");
    }

    /// <summary>
    /// Test 6 — SuperAdmin con esSuperAdmin=true puede acceder a cualquier sucursal → no 403.
    /// AjustesController.ValidarAccesoSucursal devuelve null (ok) cuando esSuperAdmin = true.
    /// </summary>
    [Fact]
    public async Task Ajustes_SuperAdmin_PuedeAccederCualquierSucursal_NoDevuelve403()
    {
        // Arrange — seed una sucursal
        var sucursal = await SeedHelper.SeedSucursalAsync(Db);

        UsarToken(TokenHelper.SuperAdmin());

        // Act — SuperAdmin accede a los ajustes de la sucursal
        var response = await Client.GetAsync($"/api/ajustes?sucursalId={sucursal.Id}");

        // Assert — no debe ser 403 (puede ser 200, 404, etc.)
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            "SuperAdmin debe poder acceder a datos de cualquier sucursal");
    }
}
