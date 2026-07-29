using System.ComponentModel.DataAnnotations;

namespace QuoteManager.Api.Models;

/// <summary>
/// The body of <c>POST /api/requests/{requestId}/quotes</c>.
/// </summary>
/// <remarks>
/// <see cref="VendorOrganizationId"/> travels in the body rather than being inferred from the
/// caller, because an Admin may draft on behalf of any vendor - <c>Request.AddQuote</c> is still
/// the sole authority on whether the caller may act for the organisation named here, so this type
/// validates only shape, never ownership.
/// </remarks>
public sealed record CreateQuoteRequest : IValidatableObject
{
    public required Guid VendorOrganizationId { get; init; }

    public required decimal Amount { get; init; }

    [Required]
    [StringLength(3, MinimumLength = 3)]
    public required string Currency { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    [StringLength(2000)]
    public string? Notes { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (VendorOrganizationId == Guid.Empty)
        {
            yield return new ValidationResult(
                "vendorOrganizationId is required.",
                [nameof(VendorOrganizationId)]);
        }

        if (Amount <= 0)
        {
            yield return new ValidationResult(
                "amount must be greater than zero.",
                [nameof(Amount)]);
        }

        if (!Currency.All(char.IsAsciiLetter))
        {
            yield return new ValidationResult(
                "currency must be a three-letter ISO-4217 code.",
                [nameof(Currency)]);
        }
    }
}
