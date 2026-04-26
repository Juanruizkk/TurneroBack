namespace TurneroApp.IntegrationTests.Infrastructure;

public static class TestConstants
{
    // Secret JWT usado en todos los tests HTTP.
    // Debe coincidir con lo inyectado en HttpTestFixture.ConfigureAppConfiguration.
    public const string JwtSecret =
        "test-secret-key-32-chars-minimum!!";

    public const string SuperAdminEmail    = "sa@test.com";
    public const string SuperAdminPassword = "SuperAdminPass!";
}
