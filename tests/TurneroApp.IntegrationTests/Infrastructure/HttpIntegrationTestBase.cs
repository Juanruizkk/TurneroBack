using System.Net.Http.Headers;
using TurneroApp.Infrastructure.Data;
using Xunit;

namespace TurneroApp.IntegrationTests.Infrastructure;

/// <summary>
/// Clase base para todos los tests HTTP que usan <see cref="HttpTestFixture"/>.
/// Garantiza una DB limpia y un <see cref="HttpClient"/> fresco por cada test.
/// </summary>
[Collection("HttpTests")]
public abstract class HttpIntegrationTestBase : IAsyncLifetime
{
    protected readonly HttpTestFixture Factory;
    protected HttpClient Client = default!;
    protected AppDbContext Db   = default!;

    protected HttpIntegrationTestBase(HttpTestFixture factory)
        => Factory = factory;

    public async Task InitializeAsync()
    {
        await Factory.ResetDbAsync();        // DB limpia antes de cada test
        // Usar https:// para evitar que UseHttpsRedirection (activo en entornos no-Development)
        // redirija a HTTPS y el HttpClient descarte el header Authorization en el redirect.
        Client = Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
        });     // HttpClient fresco
        Db     = Factory.CreateDbContext();  // DbContext fresco para seed/assert
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await Db.DisposeAsync();
    }

    // ── Helpers de autenticación ────────────────────────────────────────────

    /// <summary>Configura el Client con un Bearer token.</summary>
    protected void UsarToken(string token) =>
        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

    /// <summary>Quita cualquier token del Client (request anónimo).</summary>
    protected void SinToken() =>
        Client.DefaultRequestHeaders.Authorization = null;
}
