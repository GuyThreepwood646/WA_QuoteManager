using System.ComponentModel.DataAnnotations;
using QuoteManager.Domain.Quotes;

namespace QuoteManager.Api.Models;

/// <summary>
/// The body of <c>POST /api/requests/{requestId}/quotes/{quoteId}/transitions</c>.
/// <see cref="Validate"/> rejects an out-of-range <see cref="QuoteAction"/> that
/// <see cref="System.Text.Json.Serialization.JsonStringEnumConverter"/> would otherwise accept (AD-8).
/// </summary>
public sealed record ApplyQuoteActionRequest : IValidatableObject
{
    public required QuoteAction Action { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enum.IsDefined(Action))
        {
            yield return new ValidationResult(
                "Action is not a recognised quote action.",
                [nameof(Action)]);
        }
    }
}
