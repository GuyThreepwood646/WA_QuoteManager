using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace QuoteManager.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef migrations</c> construct a context without starting the API.
/// </summary>
/// <remarks>
/// Migrations are generated against this throwaway connection string rather than the running
/// application's, so scaffolding a migration never touches the demo database and never needs the
/// API's configuration to be valid.
/// </remarks>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<QuoteManagerDbContext>
{
    public QuoteManagerDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<QuoteManagerDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;

        return new QuoteManagerDbContext(options);
    }
}
