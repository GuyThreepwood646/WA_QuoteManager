using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuoteManager.Domain.Quotes;
using QuoteManager.Infrastructure.Persistence.Entities;

namespace QuoteManager.Infrastructure.Persistence.Configurations;

public sealed class QuoteStatusLookupConfiguration : IEntityTypeConfiguration<QuoteStatusLookup>
{
    public void Configure(EntityTypeBuilder<QuoteStatusLookup> builder)
    {
        builder.ToTable("QuoteStatuses");
        builder.HasKey(s => s.Status);

        // Same conversion and width as Quotes.Status, so the two columns are byte-for-byte
        // comparable and the foreign key is a text-to-text match.
        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

        // Seeded into the migration rather than by the application seeder. The Quotes.Status
        // foreign key depends on these rows existing, so a database created by `dotnet ef database
        // update` alone — with the app never started — must already be valid.
        builder.HasData(Enum.GetValues<QuoteStatus>().Select(status => new QuoteStatusLookup
        {
            Status = status,
            DisplayOrder = (int)status,
            IsTerminal = QuoteTransitions.IsTerminal(status),
        }));
    }
}
