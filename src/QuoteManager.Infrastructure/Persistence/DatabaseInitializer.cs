using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace QuoteManager.Infrastructure.Persistence;

/// <summary>
/// Brings the database up to date at start-up and seeds it when the environment permits.
/// <c>EnsureCreated</c> is never used — it would skip migration history and silently omit AD-3's
/// filtered unique index (AD-16).
/// </summary>
public sealed class DatabaseInitializer(
    QuoteManagerDbContext context,
    DemoDataSeeder seeder,
    IHostEnvironment environment,
    ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

        // Guarded because joining the migration names is work the log level may not want done.
        if (logger.IsEnabled(LogLevel.Information))
        {
            if (pending.Length > 0)
            {
                var names = string.Join(", ", pending);
                PersistenceLog.ApplyingMigrations(logger, pending.Length, names);
            }
            else
            {
                PersistenceLog.SchemaUpToDate(logger);
            }
        }

        await context.Database.MigrateAsync(cancellationToken);

        // Confined to non-production: a seeder that can run anywhere is one configuration mistake
        // away from writing demo data into a real database.
        if (environment.IsProduction())
        {
            PersistenceLog.SkippingSeedInProduction(logger);
            return;
        }

        await seeder.SeedAsync(cancellationToken);
    }
}
