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

    [StringLength(500)]
    public string? PrimaryAddress { get; init; }

    [StringLength(200)]
    public string? PrimaryContactName { get; init; }

    [StringLength(320)]
    [EmailAddress]
    public string? PrimaryContactEmail { get; init; }

    [StringLength(50)]
    public string? PrimaryContactPhone { get; init; }

    public bool IsPreferredVendor { get; init; }

    public OrganizationLocationRequest[] Locations { get; init; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enum.IsDefined(Kind))
        {
            yield return new ValidationResult(
                "kind is not a recognised organization kind.",
                [nameof(Kind)]);
        }

        if (Kind != OrganizationKind.Vendor && IsPreferredVendor)
        {
            yield return new ValidationResult(
                "Only vendor organizations can be marked as preferred.",
                [nameof(IsPreferredVendor)]);
        }
    }
}
