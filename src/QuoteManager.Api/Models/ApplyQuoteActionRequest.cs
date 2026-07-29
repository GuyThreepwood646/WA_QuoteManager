using System.ComponentModel.DataAnnotations;
using QuoteManager.Domain.Quotes;

namespace QuoteManager.Api.Models;

/// <summary>
/// The body of <c>POST /api/requests/{requestId}/quotes/{quoteId}/transitions</c>.
/// </summary>
/// <remarks>
/// The one action-driven transition contract. There is no attribute in
/// <see cref="System.ComponentModel.DataAnnotations"/> that constrains an enum to its defined
/// members, and <see cref="System.Text.Json.Serialization.JsonStringEnumConverter"/> still accepts
/// a bare integer on deserialisation — so <c>{"action": 99}</c> would otherwise bind successfully
/// to an undefined <see cref="QuoteAction"/> value and fall through every case in
/// <see cref="QuoteTransitions"/>'s table, surfacing as an opaque 409 instead of a 400 that names
/// the actual problem. <see cref="Validate"/> closes that gap at the boundary, before it ever
/// reaches domain code.
/// </remarks>
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
