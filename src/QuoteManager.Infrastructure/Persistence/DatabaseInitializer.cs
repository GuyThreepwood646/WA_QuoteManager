using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace QuoteManager.Infrastructure.Persistence;

/// <summary>
/// Brings the database up to date at start-up, and seeds it when the environment permits.
/// </summary>
/// <remarks>
/// Migrations are the only schema authority. <c>EnsureCreated</c> is deliberately not used
/// anywhere — it builds a schema from the model while skipping migration history, which would
/// silently omit the filtered unique index the accepted-quote invariant depends on.
/// </remarks>
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

        // Seeding is confined to non-production environments. A seeder that can run anywhere is
        // one configuration mistake away from writing demo organisations into real data.
        if (environment.IsProduction())
        {
            PersistenceLog.SkippingSeedInProduction(logger);
            return;
        }

        await seeder.SeedAsync(cancellationToken);
    }
}
