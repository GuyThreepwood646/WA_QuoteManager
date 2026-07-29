namespace QuoteManager.Domain.Organizations;

/// <summary>
/// A location entry supplied when creating or updating an organization profile.
/// </summary>
public sealed record OrganizationLocationInput(string Address, string? Phone);
