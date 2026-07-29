using System.ComponentModel.DataAnnotations;

namespace QuoteManager.Api.Models;

/// <summary>
/// The body of <c>POST /api/auth/login</c>.
/// </summary>
/// <remarks>
/// The one login contract the SPA's apiClient depends on.
///
/// Property attributes reject what a shape check alone can catch — a missing field, a value that
/// isn't an email at all. <see cref="Validate"/> exists for the one thing attributes can't express
/// here: <see cref="EmailAddressAttribute"/> matches on the presence of a single interior <c>@</c>
/// and doesn't anchor to the string's edges, so <c>" admin@warehouseanywhere.test "</c> passes it
/// — and would silently fail the exact-match lookup in
/// <see cref="QuoteManager.Api.Auth.AuthEndpoints"/> with a generic "invalid credentials" instead
/// of a clear, specific 400.
/// </remarks>
public sealed record LoginRequest : IValidatableObject
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Email.Trim() != Email)
        {
            yield return new ValidationResult(
                "Email must not have leading or trailing whitespace.",
                [nameof(Email)]);
        }
    }
}
