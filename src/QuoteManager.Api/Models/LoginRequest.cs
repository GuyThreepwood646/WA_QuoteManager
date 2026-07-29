using System.ComponentModel.DataAnnotations;

namespace QuoteManager.Api.Models;

/// <summary>
/// The body of <c>POST /api/auth/login</c>.
/// </summary>
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
