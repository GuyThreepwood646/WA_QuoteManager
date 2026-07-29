using QuoteManager.Domain.Identity;

namespace QuoteManager.Infrastructure.Identity;

/// <summary>
/// A user account.
/// </summary>
/// <remarks>
/// Authentication is an infrastructure concern, so the Domain has no user entity — it has
/// <see cref="DomainActor"/>, which carries only the identity and roles a business rule needs.
/// Keeping the password hash out of the Domain is the point: an aggregate should be impossible to
/// misuse as a credential store.
/// </remarks>
public sealed class AppUser
{
    public Guid Id { get; init; }

    public required string Email { get; init; }

    public required string DisplayName { get; init; }

    public required string PasswordHash { get; set; }

    public AppRole Roles { get; init; }

    /// <summary>
    /// The organization this user acts for, or null for staff who act for the platform.
    /// </summary>
    public Guid? OrganizationId { get; init; }

    public DomainActor ToActor() => new(Id, DisplayName, Roles, OrganizationId);
}
