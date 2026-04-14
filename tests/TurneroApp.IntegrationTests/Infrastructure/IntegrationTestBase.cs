using TurneroApp.Infrastructure.Data;
using Xunit;

namespace TurneroApp.IntegrationTests.Infrastructure;

public abstract class IntegrationTestBase : IAsyncLifetime, IClassFixture<PostgresFixture>
{
    protected readonly PostgresFixture Fixture;
    protected AppDbContext Db = default!;

    protected IntegrationTestBase(PostgresFixture fixture) => Fixture = fixture;

    public async Task InitializeAsync()
    {
        await Fixture.ResetAsync();      // DB limpia antes de cada test
        Db = Fixture.CreateDbContext();  // DbContext fresco por test
    }

    public async Task DisposeAsync() => await Db.DisposeAsync();
}
