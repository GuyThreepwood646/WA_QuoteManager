using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuoteManager.Domain.Organizations;
using QuoteManager.Domain.Quotes;
using QuoteManager.Infrastructure.Persistence.Entities;

namespace QuoteManager.Infrastructure.Persistence.Configurations;

public sealed class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    private const decimal MinorUnitsPerUnit = 100m;

    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.ToTable("Quotes");
        builder.HasKey(q => q.Id);

        builder.Property(q => q.Version).IsConcurrencyToken();

        // Must stay text, not EF's default ordinal int — AD-3's filtered index depends on it.
        builder.Property(q => q.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.ComplexProperty(q => q.Amount, money =>
        {
            // SQLite has no decimal type; EF stores decimal as TEXT, which sorts lexicographically
            // (9.00 above 10.00), so amounts are stored as integer minor units instead.
            money.Property(m => m.Amount)
                .HasColumnName("AmountMinorUnits")
                .HasConversion(
                    amount => (long)decimal.Round(amount * MinorUnitsPerUnit, 0, MidpointRounding.ToEven),
                    minorUnits => minorUnits / MinorUnitsPerUnit)
                .IsRequired();

            money.Property(m => m.CurrencyCode)
                .HasColumnName("CurrencyCode")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(q => q.Notes).HasMaxLength(2000);
        builder.Property(q => q.StatusReason).HasMaxLength(200);

        builder.HasIndex(q => q.RequestId);
        builder.HasIndex(q => q.VendorOrganizationId);

        // Referential integrity for the two identifiers this table carries. Both were previously
        // indexed but unconstrained, which let the database accept a quote from a vendor that does
        // not exist — an index makes a lookup fast, it does not make a value valid.
        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(q => q.VendorOrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<QuoteStatusLookup>()
            .WithMany()
            .HasForeignKey(q => q.Status)
            .HasPrincipalKey(s => s.Status)
            .OnDelete(DeleteBehavior.Restrict);

        // The dashboard's two hottest reads: what is awaiting review, and what lapses soon.
        builder.HasIndex(q => new { q.Status, q.ExpiresAt });

        // AD-3: the database-side half of "guarded in the aggregate and again at the database".
        builder.HasIndex(q => q.RequestId)
            .IsUnique()
            .HasFilter("\"Status\" = 'Accepted'")
            .HasDatabaseName("UX_Quotes_OneAcceptedPerRequest");
    }
}
