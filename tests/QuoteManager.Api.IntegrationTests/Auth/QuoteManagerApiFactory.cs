using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;

namespace QuoteManager.Api.IntegrationTests.Auth;

/// <summary>
/// Hosts the real API against an isolated, throwaway SQLite database per test class, so auth tests
/// exercise the actual middleware pipeline rather than a hand-rolled substitute.
/// </summary>
public sealed class QuoteManagerApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"qm-api-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Explicit rather than relying on the ambient ASPNETCORE_ENVIRONMENT: the seeder is
        // skipped in Production, and these tests need the seeded demo accounts to log in against.
        builder.UseEnvironment(Environments.Development);

        // UseSetting, not ConfigureAppConfiguration: Program.cs is a minimal-hosting entry point
        // that calls app.Run() directly rather than exposing a testable CreateHostBuilder, so
        // WebApplicationFactory's ConfigureAppConfiguration hook is not guaranteed to run before
        // the real server CreateClient() builds reads configuration - it silently no-ops for that
        // code path while still applying to a bare Services access, which is worse than either
        // consistently working or consistently failing. UseSetting writes directly into the
        // in-memory settings WebApplicationFactory itself uses to seed the host, so it applies
        // regardless of which path built the host. Discovered because every "isolated" test was
        // actually reading and mutating one shared quotemanager.db in the test bin directory.
        builder.UseSetting("ConnectionStrings:QuoteManager", $"Data Source={_databasePath}");
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
