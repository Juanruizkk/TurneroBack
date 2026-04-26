using System.Data.Common;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;
using TurneroApp.Infrastructure.Data;
using TurneroApp.Infrastructure.MultiTenant;
using Xunit;

namespace TurneroApp.IntegrationTests.Infrastructure;

/// <summary>
/// WebApplicationFactory que levanta la API real en memoria contra un contenedor Postgres.
/// Compartida entre todos los tests de la colección "HttpTests" via ICollectionFixture.
/// </summary>
public class HttpTestFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("turnero_http_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private Respawner    _respawner  = default!;
    private DbConnection _connection = default!;

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

        // PostConfigure garantiza que los parámetros JWT usen el secret de test,
        // independientemente del orden en que Program.cs lee la configuración al arrancar.
        // Sin esto, el app puede leer Jwt:Audience = "TurneroAppClients" (appsettings.json)
        // mientras los tokens se generan con audience = "TurneroApp" → 401 en todos los tests autenticados.
        builder.ConfigureServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestConstants.JwtSecret));
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer           = true,
                    ValidateAudience         = true,
                    ValidateLifetime         = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer              = "TurneroApp",
                    ValidAudience            = "TurneroApp",
                    IssuerSigningKey         = key,
                };
            });
        });

        builder.ConfigureServices(ConfigureTestServices);
    }

    /// <summary>
    /// Hook para que subclases puedan reemplazar servicios de producción.
    /// Por defecto no hace nada: el rate limiter de producción queda activo,
    /// pero los tests HTTP generales nunca hacen ≥10 intentos de login
    /// así que no se dispara el rate limiter.
    /// </summary>
    protected virtual void ConfigureTestServices(IServiceCollection services) { }

    // ── Ciclo de vida ───────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        // Npgsql 8 es estricto con DateTimeKind en columnas "timestamp without time zone"
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        await _container.StartAsync();

        // Aplicar migraciones (el auto-migrate de Program.cs no corre en "Testing")
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
        }

        // Conexión persistente para Respawn
        _connection = new NpgsqlConnection(_container.GetConnectionString());
        await _connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(_connection, new RespawnerOptions
        {
            DbAdapter        = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
        });
    }

    public new async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        await _container.DisposeAsync();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>Trunca todas las tablas de datos antes de cada test.</summary>
    public async Task ResetDbAsync() => await _respawner.ResetAsync(_connection);

    /// <summary>
    /// Crea un AppDbContext fresco para seed y asserts directos fuera del pipeline HTTP.
    /// Usa NullTenantContext (SucursalId = null) para desactivar los global query filters
    /// multi-tenant, igual que PostgresFixture.CreateDbContext().
    /// </summary>
    public AppDbContext CreateDbContext()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;
        return new AppDbContext(opts, new NullTenantContext());
    }

    private sealed class NullTenantContext : ITenantContext
    {
        public Guid? SucursalId => null;
    }
}

/// <summary>
/// Registra HttpTestFixture como fixture compartido para la colección "HttpTests".
/// Todos los tests de esta colección comparten el mismo contenedor Postgres y TestServer.
/// </summary>
[CollectionDefinition("HttpTests")]
public class HttpTestsCollection : ICollectionFixture<HttpTestFixture> { }
