# 02 — Tests de Autorización HTTP

**Hallazgos cubiertos:** C-02 · A-01 · A-02 · A-06 · M-06
**Proyecto:** `TurneroApp.IntegrationTests`
**Carpeta:** `Security/`
**Clases:** `AutorizacionHttpTests.cs` · `IdorHttpTests.cs` · `RateLimitingTests.cs`

---

## `AutorizacionHttpTests.cs`

Cubre C-02 (SuperAdminController sin `[Authorize]`), A-01 (controllers sin `[Authorize]`),
y A-06 (campo `Origen` en creación de turnos).

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TurneroApp.Domain.Enums;
using TurneroApp.IntegrationTests.Infrastructure;
using Xunit;

namespace TurneroApp.IntegrationTests.Security;

[Collection("HttpTests")]
public class AutorizacionHttpTests : HttpIntegrationTestBase
{
    public AutorizacionHttpTests(HttpTestFixture factory) : base(factory) { }

    // ── C-02: SuperAdminController ──────────────────────────────────────────

    /// <summary>Test 1 — SuperAdminController sin token debe devolver 401.</summary>
    [Fact]
    public async Task SuperAdmin_SinToken_Devuelve401()
    {
        SinToken();
        var response = await Client.GetAsync("/api/superadmin/sucursales");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Test 2 — SuperAdminController con token de usuario normal debe devolver 403.</summary>
    [Fact]
    public async Task SuperAdmin_ConTokenNormal_Devuelve403()
    {
        UsarToken(TokenHelper.GenerarToken(esSuperAdmin: false));
        var response = await Client.GetAsync("/api/superadmin/sucursales");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── A-01: Endpoints de staff requieren autenticación ───────────────────

    /// <summary>Test 3 — Endpoints de staff sin token deben devolver 401.</summary>
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

    /// <summary>Test 4 — Endpoints públicos sin token NO deben devolver 401.</summary>
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

    // ── A-06: Campo Origen en creación de turnos ───────────────────────────

    /// <summary>Test 7 — Turno creado sin token debe tener Origen = Web.</summary>
    [Fact]
    public async Task CrearTurno_SinToken_OrigenEsWeb()
    {
        // Arrange — seed completo
        var (sucursalId, clienteId, servicioId, profId) =
            await SeedTurnoPrerequisitosAsync();

        SinToken();

        var dto = new
        {
            sucursalId,
            clienteId,
            servicioId,
            profesionalId   = profId,
            fechaHoraInicio = DateTime.UtcNow.AddDays(1).Date.AddHours(10)
        };
        var content = new StringContent(
            JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");

        // Act
        var response = await Client.PostAsync("/api/turnos", content);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Created, HttpStatusCode.OK);

        var turno = await Db.Turnos.SingleAsync();
        turno.Origen.Should().Be(OrigenTurno.Web,
            "un request sin token debe registrarse con Origen = Web");
    }

    /// <summary>Test 8 — Turno creado con token debe tener Origen = Administrativo.</summary>
    [Fact]
    public async Task CrearTurno_ConToken_OrigenEsAdministrativo()
    {
        // Arrange
        var (sucursalId, clienteId, servicioId, profId) =
            await SeedTurnoPrerequisitosAsync();

        UsarToken(TokenHelper.ParaSucursal(sucursalId));

        var dto = new
        {
            sucursalId,
            clienteId,
            servicioId,
            profesionalId   = profId,
            fechaHoraInicio = DateTime.UtcNow.AddDays(1).Date.AddHours(10)
        };
        var content = new StringContent(
            JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");

        // Act
        var response = await Client.PostAsync("/api/turnos", content);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Created, HttpStatusCode.OK);

        var turno = await Db.Turnos.SingleAsync();
        turno.Origen.Should().Be(OrigenTurno.Administrativo,
            "un request con token debe registrarse con Origen = Administrativo");
    }

    // ── Helpers de seed ─────────────────────────────────────────────────────

    private async Task<(Guid sucursalId, Guid clienteId, Guid servicioId, Guid profId)>
        SeedTurnoPrerequisitosAsync()
    {
        // Ver SeedHelper en Helpers/ para la implementación completa.
        // Se necesita: Sucursal, Cliente, Servicio activo, UsuarioSucursalRol (Profesional).
        var sucursal  = await SeedHelper.SeedSucursalAsync(Db);
        var cliente   = await SeedHelper.SeedClienteAsync(Db, sucursal.Id);
        var servicio  = await SeedHelper.SeedServicioAsync(Db, sucursal.Id, activo: true);
        var prof      = await SeedHelper.SeedProfesionalAsync(Db, sucursal.Id);

        return (sucursal.Id, cliente.Id, servicio.Id, prof.UsuarioId);
    }
}
```

---

## `IdorHttpTests.cs`

Cubre A-02 (IDOR — acceso a datos de otra sucursal).

```csharp
using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using TurneroApp.IntegrationTests.Infrastructure;
using Xunit;

namespace TurneroApp.IntegrationTests.Security;

[Collection("HttpTests")]
public class IdorHttpTests : HttpIntegrationTestBase
{
    public IdorHttpTests(HttpTestFixture factory) : base(factory) { }

    /// <summary>
    /// Test 5 — Token de sucursal A intentando acceder a ajustes de sucursal B → 403.
    /// Verifica que el middleware/filtro de autorización por sucursal rechace el acceso.
    /// </summary>
    [Fact]
    public async Task Ajustes_TokenSucursalA_AccedeSucursalB_Devuelve403()
    {
        // Arrange — dos sucursales distintas en la DB
        var sucursalA = await SeedHelper.SeedSucursalAsync(Db);
        var sucursalB = await SeedHelper.SeedSucursalAsync(Db);

        // Token firmado para sucursal A
        UsarToken(TokenHelper.ParaSucursal(sucursalA.Id));

        // Act — intenta acceder a los ajustes de sucursal B
        var response = await Client.GetAsync($"/api/ajustes?sucursalId={sucursalB.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "un usuario de sucursal A no puede acceder a datos de sucursal B");
    }

    /// <summary>
    /// Test 6 — SuperAdmin puede acceder a cualquier sucursal → no 403.
    /// El claim esSuperAdmin=true debe bypassear la restricción de sucursal.
    /// </summary>
    [Fact]
    public async Task Ajustes_SuperAdmin_PuedeAccederCualquierSucursal_NoDevuelve403()
    {
        // Arrange — seed una sucursal real
        var sucursal = await SeedHelper.SeedSucursalAsync(Db);

        UsarToken(TokenHelper.SuperAdmin());

        // Act
        var response = await Client.GetAsync($"/api/ajustes?sucursalId={sucursal.Id}");

        // Assert — SuperAdmin no recibe 403
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            "SuperAdmin debe poder acceder a datos de cualquier sucursal");
    }
}
```

---

## `RateLimitingTests.cs`

Cubre M-06 (rate limiting en `/api/auth/login`).

**Importante:** Usa `RateLimitTestFixture` — una subclase de `HttpTestFixture` que **NO**
deshabilita el rate limiter. Esto permite testear el comportamiento real de rate limiting
sin afectar a los demás tests.

```csharp
using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using TurneroApp.IntegrationTests.Infrastructure;
using Xunit;

namespace TurneroApp.IntegrationTests.Security;

/// <summary>
/// Fixture especial que preserva el rate limiter real de la aplicación.
/// No reemplaza el rate limiter en ConfigureServices.
/// </summary>
public sealed class RateLimitTestFixture : HttpTestFixture
{
    // Hereda todo de HttpTestFixture pero sin el override del rate limiter.
    // Sobreescribir ConfigureWebHost para NO llamar AddRateLimiter permisivo:
    //
    // protected override void ConfigureWebHost(IWebHostBuilder builder)
    // {
    //     builder.UseEnvironment("Testing");
    //     builder.ConfigureAppConfiguration(...); // igual que el padre
    //     // SIN el ConfigureServices que reemplaza el rate limiter
    // }
    //
    // Ver implementación en Infrastructure/RateLimitTestFixture.cs
}

[CollectionDefinition("RateLimitTests")]
public class RateLimitTestsCollection : ICollectionFixture<RateLimitTestFixture> { }

[Collection("RateLimitTests")]
public class RateLimitingTests : HttpIntegrationTestBase
{
    // Client que usa el fixture con rate limiter activo
    protected HttpClient RateLimitClient = default!;

    public RateLimitingTests(RateLimitTestFixture factory) : base(factory)
    {
        RateLimitClient = factory.CreateClient();
    }

    /// <summary>
    /// Test 9 — El endpoint de login permite hasta 10 intentos; el 11mo devuelve 429.
    /// Verifica que M-06 (rate limiting por IP en /api/auth/login) esté activo.
    /// </summary>
    [Fact]
    public async Task Login_MasDe10Intentos_Devuelve429()
    {
        HttpResponseMessage? ultimaRespuesta = null;

        for (int i = 0; i < 11; i++)
        {
            // HttpContent no se puede reenviar — crear instancia nueva por iteración
            var content = new StringContent(
                JsonSerializer.Serialize(new { email = "x@x.com", password = "wrong" }),
                Encoding.UTF8, "application/json");

            ultimaRespuesta = await RateLimitClient.PostAsync("/api/auth/login", content);
        }

        ultimaRespuesta!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "el rate limiter debe rechazar el 11mo intento de login con 429");
    }

    /// <summary>
    /// Test 10 — El rate limiter SOLO aplica a /api/auth. Otros endpoints no quedan bloqueados.
    /// Envía 15 requests concurrentes a un endpoint autenticado y verifica que ninguno devuelva 429.
    /// </summary>
    [Fact]
    public async Task EndpointsNoAuth_SinRateLimit_NoDevuelven429()
    {
        RateLimitClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", TokenHelper.GenerarToken());

        var tasks = Enumerable.Range(0, 15)
            .Select(_ => RateLimitClient.GetAsync("/api/sucursales"));

        var responses = await Task.WhenAll(tasks);

        responses.Should().NotContain(
            r => r.StatusCode == HttpStatusCode.TooManyRequests,
            "el rate limiter solo aplica a /api/auth/login, no a otros endpoints");
    }
}
```

---

## Consideraciones de implementación

### Endpoint `/api/ajustes` — parámetro `sucursalId`

El test IDOR (Test 5) asume que el endpoint acepta `sucursalId` como query param y que el
middleware/filter verifica que `sucursalId` del claim coincida con el del request.

Si el endpoint usa otra forma de identificar la sucursal (ej: route param `/api/ajustes/{sucursalId}`),
ajustar la URL en el test.

### Rate limiter — política por IP vs por usuario

El Test 9 funciona porque `WebApplicationFactory.CreateClient()` envía todos los requests
desde la misma IP de loopback (`127.0.0.1`). Si el rate limiter usa el `userId` del claim
en lugar de la IP, el test debe:
1. No enviar token (usar endpoint sin autenticación) — aplica si el login no requiere auth previa.
2. O usar el mismo token en todos los requests y verificar que la política sea por usuario.

### SuperAdmin claim

El filtro de autorización por sucursal debe leer el claim `esSuperAdmin` y omitir la
validación de `sucursalId` cuando sea `true`. Verificar que `AuthorizationHandler` o
el atributo custom lo implementen así.
