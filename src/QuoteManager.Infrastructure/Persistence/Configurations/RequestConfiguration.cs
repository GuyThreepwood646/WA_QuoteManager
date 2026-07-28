using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuoteManager.Domain.Requests;

namespace QuoteManager.Infrastructure.Persistence.Configurations;

public sealed class RequestConfiguration : IEntityTypeConfiguration<Request>
{
    public void Configure(EntityTypeBuilder<Request> builder)
    {
        builder.ToTable("Requests");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Version).IsConcurrencyToken();

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(r => r.Title).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(4000);

        builder.HasIndex(r => r.ClientOrganizationId);
        builder.HasIndex(r => r.Status);

        // Quotes are part of the request aggregate, so they load and save with it and are reached
        // only through it. The backing field is the collection the aggregate actually mutates;
        // exposing the public IReadOnlyList to EF would let it write through a read-only surface.
        builder.HasMany(r => r.Quotes)
            .WithOne()
            .HasForeignKey(q => q.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(r => r.Quotes)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .AutoInclude();

        // Domain events are transient: they exist to be drained by the interceptor on save and
        // must never become a column.
        builder.Ignore(r => r.DomainEvents);
    }
}
