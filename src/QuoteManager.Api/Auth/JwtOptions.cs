namespace QuoteManager.Api.Auth;

/// <summary>
/// Binds the <c>Jwt</c> configuration section that backs the bearer scheme.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// HS256 signing key. Committed in <c>appsettings.json</c> rather than a secret store, matching
    /// this build's other demo credentials (the seeded password is published in the README): there
    /// is no real user data behind it and the deliverable must run unmodified from a fresh clone.
    /// </summary>
    public required string SigningKey { get; init; }

    public required string Issuer { get; init; }

    public required string Audience { get; init; }

    /// <summary>
    /// Long enough that expiry can't interrupt a demo or a reviewer's evaluation session.
    /// </summary>
    public TimeSpan Lifetime { get; init; } = TimeSpan.FromHours(8);
}
