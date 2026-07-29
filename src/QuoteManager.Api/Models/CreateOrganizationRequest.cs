using System.ComponentModel.DataAnnotations;
using QuoteManager.Domain.Organizations;

namespace QuoteManager.Api.Models;

/// <summary>
/// The body of <c>POST /api/organizations</c>.
/// </summary>
public sealed record CreateOrganizationRequest : IValidatableObject
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public required string Name { get; init; }

    public required OrganizationKind Kind { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enum.IsDefined(Kind))
        {
            yield return new ValidationResult(
                "kind is not a recognised organization kind.",
                [nameof(Kind)]);
        }
    }
}
