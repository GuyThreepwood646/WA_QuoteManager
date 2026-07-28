using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuoteManager.Domain.Organizations;
using QuoteManager.Domain.Requests;

namespace QuoteManager.Infrastructure.Persistence.Configurations;

public sealed class RequestInvitationConfiguration : IEntityTypeConfiguration<RequestInvitation>
{
    public void Configure(EntityTypeBuilder<RequestInvitation> builder)
    {
        builder.ToTable("RequestInvitations");

        // Composite natural key rather than a surrogate: the pair is the fact being recorded, and
        // making it the key means the database itself rejects inviting the same vendor twice.
        builder.HasKey(i => new { i.RequestId, i.VendorOrganizationId });

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(i => i.VendorOrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        // Answers "what have I been invited to?" from the vendor's side, which the reverse of the
        // composite key cannot serve.
        builder.HasIndex(i => i.VendorOrganizationId);
    }
}
