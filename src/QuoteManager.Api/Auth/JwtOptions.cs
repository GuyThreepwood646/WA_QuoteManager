namespace QuoteManager.Api.Auth;

/// <summary>
/// Binds the <c>Jwt</c> configuration section that backs the bearer scheme.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// HS256 signing key.
    /// </summary>
    public required string SigningKey { get; init; }

    public required string Issuer { get; init; }

    public required string Audience { get; init; }

    public TimeSpan Lifetime { get; init; } = TimeSpan.FromHours(8);
}
