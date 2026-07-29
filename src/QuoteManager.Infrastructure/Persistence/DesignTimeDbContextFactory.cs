using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace QuoteManager.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef migrations</c> construct a context without starting the API, using a
/// throwaway connection string so scaffolding never touches the demo database.
/// </summary>
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
