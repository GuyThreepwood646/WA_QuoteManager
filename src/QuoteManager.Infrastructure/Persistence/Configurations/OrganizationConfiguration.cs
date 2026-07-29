using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuoteManager.Domain.Organizations;

namespace QuoteManager.Infrastructure.Persistence.Configurations;

public sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("Organizations");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Version).IsConcurrencyToken();

        builder.Property(o => o.Kind)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(o => o.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(o => o.Name).IsUnique();

        builder.Property(o => o.RetiredAt);
        builder.Property(o => o.PrimaryAddress).HasMaxLength(500);
        builder.Property(o => o.PrimaryContactName).HasMaxLength(200);
        builder.Property(o => o.PrimaryContactEmail).HasMaxLength(320);
        builder.Property(o => o.PrimaryContactPhone).HasMaxLength(50);
        builder.Property(o => o.IsPreferredVendor).HasDefaultValue(false);

        builder.HasMany(o => o.Locations)
            .WithOne()
            .HasForeignKey(l => l.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(o => o.Locations)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(o => o.DomainEvents);
    }
}
