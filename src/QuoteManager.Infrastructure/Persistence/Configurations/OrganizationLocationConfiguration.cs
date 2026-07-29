using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuoteManager.Domain.Organizations;

namespace QuoteManager.Infrastructure.Persistence.Configurations;

public sealed class OrganizationLocationConfiguration : IEntityTypeConfiguration<OrganizationLocation>
{
    public void Configure(EntityTypeBuilder<OrganizationLocation> builder)
    {
        builder.ToTable("OrganizationLocations");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Version);
        builder.Property(l => l.Address).HasMaxLength(500).IsRequired();
        builder.Property(l => l.Phone).HasMaxLength(50);
        builder.Property(l => l.SortOrder).IsRequired();

        builder.HasIndex(l => l.OrganizationId);
    }
}
