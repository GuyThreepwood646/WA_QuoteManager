using System.ComponentModel.DataAnnotations;

namespace QuoteManager.Api.Models;

/// <summary>
/// The body of <c>POST /api/users/{userId}/reset-password</c>. Whether <see cref="CurrentPassword"/>
/// is actually required depends on whether the caller is resetting their own account or an admin is
/// resetting someone else's - that check needs the loaded target user, not just this body, so it
/// lives in the endpoint rather than here (the same reason duplicate-email/existence checks live in
/// endpoints elsewhere in this codebase).
/// </summary>
public sealed record ResetPasswordRequest : IValidatableObject
{
    public string? CurrentPassword { get; init; }

    [Required]
    public required string NewPassword { get; init; }

    [Required]
    public required string ConfirmNewPassword { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (NewPassword != ConfirmNewPassword)
        {
            yield return new ValidationResult("Passwords do not match.", [nameof(ConfirmNewPassword)]);
        }

        foreach (var failure in PasswordPolicy.Evaluate(NewPassword))
        {
            yield return new ValidationResult(failure, [nameof(NewPassword)]);
        }
    }
}
