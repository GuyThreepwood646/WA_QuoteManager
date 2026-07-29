using System.ComponentModel.DataAnnotations;

namespace QuoteManager.Api.Models;

/// <summary>
/// The body of <c>POST /api/requests/{requestId}/invitations</c>.
/// </summary>
public sealed record InviteVendorRequest : IValidatableObject
{
    public required Guid VendorOrganizationId { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (VendorOrganizationId == Guid.Empty)
        {
            yield return new ValidationResult(
                "vendorOrganizationId is required.",
                [nameof(VendorOrganizationId)]);
        }
    }
}
