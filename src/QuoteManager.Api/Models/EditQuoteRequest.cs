using System.ComponentModel.DataAnnotations;

namespace QuoteManager.Api.Models;

/// <summary>
/// The body of <c>PUT /api/requests/{requestId}/quotes/{quoteId}</c>.
/// </summary>
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
                "currency must be a written in ASCII letters.",
                [nameof(Currency)]);
        }
    }
}
