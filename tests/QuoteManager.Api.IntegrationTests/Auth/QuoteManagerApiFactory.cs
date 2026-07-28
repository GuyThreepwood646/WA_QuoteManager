using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace QuoteManager.Api.IntegrationTests.Auth;

/// <summary>
/// Hosts the real API against an isolated, throwaway SQLite database per test class, so auth tests
/// exercise the actual middleware pipeline (AD-9) rather than a hand-rolled substitute.
/// </summary>
public sealed class QuoteManagerApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"qm-api-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Explicit rather than relying on the ambient ASPNETCORE_ENVIRONMENT: AD-16's seeder is
        // skipped in Production, and these tests need the seeded demo accounts to log in against.
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:QuoteManager"] = $"Data Source={_databasePath}",
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}
