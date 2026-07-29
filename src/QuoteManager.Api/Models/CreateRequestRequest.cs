using System.ComponentModel.DataAnnotations;

namespace QuoteManager.Api.Models;

/// <summary>
/// The body of <c>POST /api/requests</c>.
/// </summary>
/// <remarks>
/// Validated at the boundary via <see cref="IValidatableObject"/> rather than trusted to the
/// domain alone - <see cref="ClientOrganizationId"/> being <see cref="Guid.Empty"/> is a client
/// mistake worth a 400 naming the field, not a 404 from a lookup that was never going to find
/// anything.
/// </remarks>
public sealed record CreateRequestRequest : IValidatableObject
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public required string Title { get; init; }

    [StringLength(2000)]
    public string? Description { get; init; }

    public required Guid ClientOrganizationId { get; init; }

    public DateTimeOffset? NeededBy { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ClientOrganizationId == Guid.Empty)
        {
            yield return new ValidationResult(
                "clientOrganizationId is required.",
                [nameof(ClientOrganizationId)]);
        }
    }
}
