using QuoteManager.Domain.Identity;

namespace QuoteManager.Application.Abstractions;

/// <summary>
/// The acting user for the current unit of work, implemented over the authenticated principal
/// only — no request payload may supply an actor.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid UserId { get; }

    string DisplayName { get; }

    AppRole Roles { get; }

    Guid? OrganizationId { get; }

    DomainActor ToActor();
}
