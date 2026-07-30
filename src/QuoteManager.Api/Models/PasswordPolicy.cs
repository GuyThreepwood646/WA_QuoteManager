using System.Text.RegularExpressions;

namespace QuoteManager.Api.Models;

/// <summary>
/// The one password-complexity rule set, shared by <see cref="CreateUserRequest"/> and
/// <see cref="ResetPasswordRequest"/> so the two entry points a password can be set through can
/// never drift apart. Mirrored on the frontend by <c>lib/password-validation.ts</c>'s
/// <c>PASSWORD_REQUIREMENTS</c> - keep the two in step if either changes.
/// </summary>
public static partial class PasswordPolicy
{
    private const int MinimumLength = 8;

    /// <summary>Human-readable descriptions of every unmet requirement; empty means the password passes.</summary>
    public static IReadOnlyList<string> Evaluate(string password)
    {
        var failures = new List<string>();

        if (password.Length < MinimumLength)
        {
            failures.Add($"Must be at least {MinimumLength} characters.");
        }

        if (!UppercaseLetter().IsMatch(password))
        {
            failures.Add("Must contain an uppercase letter.");
        }

        if (!LowercaseLetter().IsMatch(password))
        {
            failures.Add("Must contain a lowercase letter.");
        }

        if (!Digit().IsMatch(password))
        {
            failures.Add("Must contain a number.");
        }

        if (!SpecialCharacter().IsMatch(password))
        {
            failures.Add("Must contain a special character.");
        }

        return failures;
    }

    [GeneratedRegex("[A-Z]")]
    private static partial Regex UppercaseLetter();

    [GeneratedRegex("[a-z]")]
    private static partial Regex LowercaseLetter();

    [GeneratedRegex(@"\d")]
    private static partial Regex Digit();

    [GeneratedRegex(@"[^A-Za-z0-9]")]
    private static partial Regex SpecialCharacter();
}
