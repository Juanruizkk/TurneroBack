using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using TurneroApp.IntegrationTests.Infrastructure;
using Xunit;

namespace TurneroApp.IntegrationTests.Security;

/// <summary>
/// Fixture especial que preserva el rate limiter real de la aplicación.
/// A diferencia de HttpTestFixture (que lo deshabilita para no interferir en
/// otros tests), este fixture NO sobreescribe el rate limiter.
/// </summary>
public sealed class RateLimitTestFixture : HttpTestFixture
{
    protected override void ConfigureTestServices(
        Microsoft.Extensions.DependencyInjection.IServiceCollection services)
    {
        // No reemplazar el rate limiter → aplica la política real de producción
    }
}

[CollectionDefinition("RateLimitTests")]
public class RateLimitTestsCollection : ICollectionFixture<RateLimitTestFixture> { }

/// <summary>
/// Tests HTTP de rate limiting (M-06).
/// Usan RateLimitTestFixture para que el rate limiter esté activo.
/// </summary>
[Collection("RateLimitTests")]
public class RateLimitingTests : HttpIntegrationTestBase
{
    // Cliente exclusivo de estos tests (con rate limiter activo)
    private readonly HttpClient _rlClient;

    public RateLimitingTests(RateLimitTestFixture factory) : base(factory)
    {
        _rlClient = factory.CreateClient();
    }

    /// <summary>
    /// Test 9 — El endpoint de login permite hasta 10 intentos por IP;
    /// el 11mo debe devolver 429 Too Many Requests.
    /// Verifica que M-06 (rate limiting en /api/auth/login) esté activo.
    /// </summary>
    [Fact]
    public async Task Login_MasDe10Intentos_Devuelve429()
    {
        HttpResponseMessage? ultimaRespuesta = null;

        for (int i = 0; i < 11; i++)
        {
            // HttpContent no se puede reenviar — crear nueva instancia por request
            var content = new StringContent(
                JsonSerializer.Serialize(new { email = "x@x.com", password = "wrong" }),
                Encoding.UTF8, "application/json");

            ultimaRespuesta = await _rlClient.PostAsync("/api/auth/login", content);
        }

        ultimaRespuesta!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "el rate limiter debe rechazar el 11mo intento de login con 429");
    }

    /// <summary>
    /// Test 10 — El rate limiter SOLO aplica a /api/auth/login.
    /// Envía 15 requests a /api/sucursales y verifica que ninguno recibe 429.
    /// </summary>
    [Fact]
    public async Task EndpointsNoAuth_SinRateLimit_NoDevuelven429()
    {
        _rlClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TokenHelper.GenerarToken());

        var tasks = Enumerable.Range(0, 15)
            .Select(_ => _rlClient.GetAsync("/api/sucursales"));

        var responses = await Task.WhenAll(tasks);

        responses.Should().NotContain(
            r => r.StatusCode == HttpStatusCode.TooManyRequests,
            "el rate limiter solo aplica a /api/auth/login, no a otros endpoints");
    }
}
