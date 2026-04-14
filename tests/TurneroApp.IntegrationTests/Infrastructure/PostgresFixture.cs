using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;
using TurneroApp.Infrastructure.Data;
using TurneroApp.Infrastructure.MultiTenant;
using Xunit;

namespace TurneroApp.IntegrationTests.Infrastructure;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("turnero_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private Respawner    _respawner  = default!;
    private DbConnection _connection = default!;

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        // Npgsql 8 es estricto con DateTimeKind: rechaza Kind=Utc en columnas
        // "timestamp without time zone". Habilitamos el comportamiento legacy
        // para que el test pueda insertar DateTime.UtcNow en esas columnas.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        await _container.StartAsync();

        // Aplicar todas las migrations al contenedor recién levantado
        var options = BuildOptions();
        await using var db = new AppDbContext(options, new NullTenantContext());
        await db.Database.MigrateAsync();

        // Conexión persistente para Respawn (reutilizada entre tests)
        _connection = new NpgsqlConnection(ConnectionString);
        await _connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(_connection, new RespawnerOptions
        {
            DbAdapter        = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
        });
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        await _container.DisposeAsync();
    }

    /// <summary>Trunca todas las tablas de datos (excepto __EFMigrationsHistory).</summary>
    public async Task ResetAsync() => await _respawner.ResetAsync(_connection);

    /// <summary>
    /// Crea un AppDbContext fresco con un ITenantContext sin SucursalId.
    /// Esto evita que los global query filters lancen NullReferenceException
    /// al evaluar _tenantContext.SucursalId cuando _tenantContext es null.
    /// </summary>
    public AppDbContext CreateDbContext() =>
        new(BuildOptions(), new NullTenantContext());

    private DbContextOptions<AppDbContext> BuildOptions() =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

    // Stub que desactiva el filtro multi-tenant (SucursalId = null → sin filtro)
    private sealed class NullTenantContext : ITenantContext
    {
        public Guid? SucursalId => null;
    }
}
