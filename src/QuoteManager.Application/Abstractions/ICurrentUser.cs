using QuoteManager.Domain.Identity;

namespace QuoteManager.Application.Abstractions;

/// <summary>
/// The acting user for the current unit of work, per AD-10.
/// </summary>
/// <remarks>
/// Implemented over the authenticated principal only. No request payload may supply an actor, so
/// every audit row traces back to a caller the server itself authenticated.
/// </remarks>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid UserId { get; }

    string DisplayName { get; }

    AppRole Roles { get; }

    Guid? OrganizationId { get; }

    DomainActor ToActor();
}
