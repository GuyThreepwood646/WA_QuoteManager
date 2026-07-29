using System.ComponentModel.DataAnnotations;

namespace QuoteManager.Api.Models;

/// <summary>
/// The body of <c>POST /api/requests/{requestId}/invitations</c>.
/// </summary>
/// <remarks>
/// Whether the target organization exists and is vendor-kind needs a database lookup, so that
/// check happens in the endpoint (mirroring <c>CreateRequestAsync</c>'s client-organization check),
/// not here - this type validates only shape.
/// </remarks>
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
