namespace QuoteManager.Infrastructure.Identity;

/// <summary>
/// Claim types beyond the standard <see cref="System.Security.Claims.ClaimTypes"/> set that the
/// JWT carries and <see cref="CurrentUser"/> reads back.
/// </summary>
public static class AppClaimTypes
{
    public const string OrganizationId = "org_id";
}
