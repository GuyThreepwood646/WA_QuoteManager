using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuoteManager.Domain.Quotes;

namespace QuoteManager.Infrastructure.Persistence.Configurations;

public sealed class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    /// <summary>
    /// Scale factor for storing money as integer minor units.
    /// </summary>
    private const decimal MinorUnitsPerUnit = 100m;

    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.ToTable("Quotes");
        builder.HasKey(q => q.Id);

        builder.Property(q => q.Version).IsConcurrencyToken();

        // AD-3 depends on this being text. EF's default is the ordinal int, against which the
        // filtered index below would compare to the literal 'Accepted' and match nothing, forever,
        // with no error — the database guarantee would silently cease to exist while every test
        // still passed, because the aggregate check catches the ordinary case first.
        builder.Property(q => q.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.ComplexProperty(q => q.Amount, money =>
        {
            // SQLite has no decimal type: EF stores decimal as TEXT, which is exact but sorts
            // lexicographically, so ordering a quote list by amount would put 9.00 above 10.00.
            // Integer minor units are exact and sort correctly.
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

        // AD-3, second line of defence. The aggregate refuses a second acceptance in memory; this
        // stops two concurrent transactions that each passed that check from both committing.
        // Note the double-quoted identifier: SQLite rejects the bracket syntax the EF Core docs
        // show for SQL Server.
        builder.HasIndex(q => q.RequestId)
            .IsUnique()
            .HasFilter("\"Status\" = 'Accepted'")
            .HasDatabaseName("UX_Quotes_OneAcceptedPerRequest");
    }
}
