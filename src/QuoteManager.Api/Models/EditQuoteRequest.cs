using System.ComponentModel.DataAnnotations;

namespace QuoteManager.Api.Models;

/// <summary>
/// The body of <c>PUT /api/requests/{requestId}/quotes/{quoteId}</c>.
/// </summary>
/// <remarks>
/// Every field is required, mirroring <see cref="CreateQuoteRequest"/> minus
/// <see cref="CreateQuoteRequest.VendorOrganizationId"/>, which is immutable once a quote exists.
/// Whether the caller may edit *this* quote at all - ownership, and that it's still in
/// <c>Draft</c> - is decided entirely by <c>Request.EditQuote</c>; this type validates only shape.
/// </remarks>
public sealed record EditQuoteRequest : IValidatableObject
{
    public required decimal Amount { get; init; }

    [Required]
    [StringLength(3, MinimumLength = 3)]
    public required string Currency { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    [StringLength(2000)]
    public string? Notes { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
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
