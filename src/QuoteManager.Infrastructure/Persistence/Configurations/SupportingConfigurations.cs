using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuoteManager.Domain.Organizations;
using QuoteManager.Infrastructure.Identity;
using QuoteManager.Infrastructure.Persistence.Entities;

namespace QuoteManager.Infrastructure.Persistence.Configurations;

public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.Address).HasMaxLength(500);
        builder.Property(u => u.Phone).HasMaxLength(50);

        // Stored as the flag names rather than an integer so the seeded rows are legible to anyone
        // inspecting the database during a demo, and so adding a role cannot renumber existing ones.
        builder.Property(u => u.Roles)
            .HasConversion<string>()
            .HasMaxLength(128)
            .IsRequired();

        // Nullable: platform staff act for no organization. Restrict rather than cascade, because
        // deleting an organization should fail loudly while its people still reference it.
        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(u => u.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntries");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.SubjectType).HasMaxLength(64).IsRequired();
        builder.Property(a => a.Action).HasMaxLength(64).IsRequired();
        builder.Property(a => a.Summary).HasMaxLength(500).IsRequired();
        builder.Property(a => a.ActorDisplayName).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Note).HasMaxLength(2000);
        builder.Property(a => a.TraceId).HasMaxLength(64);

        // The activity timeline reads by subject, newest first; the dashboard reads recent
        // activity across all subjects.
        builder.HasIndex(a => new { a.SubjectType, a.SubjectId, a.OccurredAt });
        builder.HasIndex(a => a.OccurredAt);
    }
}

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Type).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Payload).IsRequired();
        builder.Property(m => m.LastError).HasMaxLength(2000);

        // The dispatcher's only query is "undispatched, oldest first", so the index covers exactly
        // that and nothing else.
        builder.HasIndex(m => new { m.DispatchedAt, m.OccurredAt });
    }
}
