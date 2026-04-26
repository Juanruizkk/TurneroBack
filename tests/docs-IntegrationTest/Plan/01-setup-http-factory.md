# 01 — Setup: HttpTestFixture + TokenHelper + HttpIntegrationTestBase

Infraestructura HTTP para tests de integración que ejercitan el stack completo de ASP.NET Core.

---

## Archivos a crear

```
tests/TurneroApp.IntegrationTests/
  Infrastructure/
    HttpTestFixture.cs
    HttpIntegrationTestBase.cs
    TokenHelper.cs
    TestConstants.cs
```

---

## `TestConstants.cs`

Constantes compartidas entre fixtures y helpers.

```csharp
namespace TurneroApp.IntegrationTests.Infrastructure;

public static class TestConstants
{
    // Secret JWT usado en todos los tests HTTP — debe coincidir con
    // lo que se inyecta en ConfigureAppConfiguration de HttpTestFixture
    public const string JwtSecret =
        "test-secret-key-32-chars-minimum!!";

    public const string SuperAdminEmail    = "sa@test.com";
    public const string SuperAdminPassword = "SuperAdminPass!";
}
```

---

## `HttpTestFixture.cs`

`WebApplicationFactory<Program>` con Testcontainers. Se registra como `ICollectionFixture`
para que todos los tests HTTP compartan un solo contenedor Postgres y una sola instancia
de la aplicación en memoria.

```csharp
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using TurneroApp.Infrastructure.Data;
using Xunit;

namespace TurneroApp.IntegrationTests.Infrastructure;

public sealed class HttpTestFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("turnero_http_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    // ── Configuración del host ──────────────────────────────────────────────

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _container.GetConnectionString(),
                ["Jwt:Secret"]    = TestConstants.JwtSecret,
                ["Jwt:Issuer"]    = "TurneroApp",
                ["Jwt:Audience"]  = "TurneroApp",
                ["SuperAdmin:Email"]    = TestConstants.SuperAdminEmail,
                ["SuperAdmin:Password"] = TestConstants.SuperAdminPassword,
            });
        });

        builder.ConfigureServices(services =>
        {
            // Reemplazar el rate limiter por uno permisivo para no interferir
            // en tests que no prueban rate limiting específicamente.
            // RateLimitingTests usa su propia fixture que NO hace esto.
            services.AddRateLimiter(opts =>
            {
                opts.RejectionStatusCode = 429;
                // Sin políticas → ningún endpoint queda limitado
            });
        });
    }

    // ── Ciclo de vida ───────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        await _container.StartAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync() => await _container.DisposeAsync();

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Devuelve un AppDbContext fresco para seed/assert fuera del pipeline HTTP.
    /// </summary>
    public AppDbContext CreateDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }
}

/// <summary>
/// Collection definition para que todos los tests HTTP compartan el mismo fixture.
/// </summary>
[CollectionDefinition("HttpTests")]
public class HttpTestsCollection : ICollectionFixture<HttpTestFixture> { }
```

> **Nota sobre `UseEnvironment("Testing")`:** El `Program.cs` debe omitir el auto-migrate
> cuando el entorno no es `Development`. Verificar que la condición sea:
> `if (app.Environment.IsDevelopment()) { db.Database.Migrate(); }`

---

## `TokenHelper.cs`

Genera JWTs firmados con el mismo secret que `HttpTestFixture` inyecta en la app.
Los tokens son válidos para el middleware de autenticación de ASP.NET Core.

```csharp
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace TurneroApp.IntegrationTests.Infrastructure;

public static class TokenHelper
{
    /// <summary>
    /// Genera un JWT firmado con el secret de test.
    /// </summary>
    /// <param name="sucursalId">SucursalId del claim (null si no aplica).</param>
    /// <param name="rol">Rol del usuario (default: "Administrativo").</param>
    /// <param name="esSuperAdmin">Si true, el claim esSuperAdmin = "true".</param>
    public static string GenerarToken(
        Guid? sucursalId   = null,
        string rol         = "Administrativo",
        bool esSuperAdmin  = false)
    {
        var claims = new List<Claim>
        {
            new("userId",       Guid.NewGuid().ToString()),
            new("esSuperAdmin", esSuperAdmin.ToString().ToLower()),
            new(ClaimTypes.Role, rol),
        };

        if (sucursalId.HasValue)
            claims.Add(new Claim("sucursalId", sucursalId.Value.ToString()));

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestConstants.JwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer:             "TurneroApp",
            audience:           "TurneroApp",
            claims:             claims,
            expires:            DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Token de SuperAdmin (sin sucursalId).</summary>
    public static string SuperAdmin() =>
        GenerarToken(esSuperAdmin: true, rol: "SuperAdmin");

    /// <summary>Token de usuario normal para una sucursal específica.</summary>
    public static string ParaSucursal(Guid sucursalId, string rol = "Administrativo") =>
        GenerarToken(sucursalId: sucursalId, rol: rol);
}
```

---

## `HttpIntegrationTestBase.cs`

Clase base para todos los tests HTTP. Se encarga de:
- Limpiar la DB antes de cada test (via Respawn sobre el contenedor del fixture).
- Exponer `Client` (sin token) y helpers para autenticación.
- Exponer `Db` para seed y asserts directos.

```csharp
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using TurneroApp.Infrastructure.Data;
using Xunit;

namespace TurneroApp.IntegrationTests.Infrastructure;

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
        // DB limpia antes de cada test
        // Nota: Respawn sobre la conexión del contenedor HTTP
        await Factory.ResetDbAsync();

        Client = Factory.CreateClient();
        Db     = Factory.CreateDbContext();
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await Db.DisposeAsync();
    }

    // ── Helpers de autenticación ────────────────────────────────────────────

    /// <summary>Configura el Client con un Bearer token para la sucursal dada.</summary>
    protected void UsarToken(string token)
    {
        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>Quita cualquier token del Client.</summary>
    protected void SinToken()
    {
        Client.DefaultRequestHeaders.Authorization = null;
    }
}
```

> **Nota:** `HttpTestFixture.ResetDbAsync()` debe implementarse igual que
> `PostgresFixture.ResetAsync()` usando Respawn. Ver sección de cambios en el fixture.

---

## Cambio requerido en `HttpTestFixture` — agregar Respawn

Agregar al fixture para soportar `ResetDbAsync()` desde `HttpIntegrationTestBase`:

```csharp
// Agregar campos en HttpTestFixture:
private Respawner    _respawner  = default!;
private DbConnection _connection = default!;

// En InitializeAsync(), después de MigrateAsync():
_connection = new NpgsqlConnection(_container.GetConnectionString());
await _connection.OpenAsync();
_respawner = await Respawner.CreateAsync(_connection, new RespawnerOptions
{
    DbAdapter        = DbAdapter.Postgres,
    SchemasToInclude = ["public"],
});

// Método público:
public async Task ResetDbAsync() => await _respawner.ResetAsync(_connection);

// En DisposeAsync():
await _connection.DisposeAsync();
await _container.DisposeAsync();
```

---

## Cambio en `.csproj` — paquetes y referencia al proyecto Api

```xml
<!-- Agregar en TurneroApp.IntegrationTests.csproj -->
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.0" />

<ProjectReference Include="..\..\src\Api\TurneroApp.Api.csproj">
  <Properties>UseAppHost=false</Properties>
</ProjectReference>
```

`UseAppHost=false` previene que MSBuild intente generar un ejecutable nativo para el proyecto Api
cuando se referencia desde el proyecto de tests.
