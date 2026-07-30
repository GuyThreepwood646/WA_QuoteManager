namespace QuoteManager.Domain.Common;

/// <summary>
/// A refused business operation. <see cref="Code"/> crosses the wire as the problem details
/// <c>code</c> extension and is the only thing the UI should branch on — the message is not a contract.
/// </summary>
public abstract class DomainException(string message) : Exception(message)
{
    public abstract string Code { get; }
}

public sealed class QuoteTransitionNotAllowedException(
    QuoteStatusName from,
    string action,
    bool blockedByRole)
    : DomainException(blockedByRole
        ? $"The current user's roles do not permit '{action}' on a quote in state '{from.Value}'."
        : $"'{action}' is not a legal action on a quote in state '{from.Value}'.")
{
    public override string Code => blockedByRole
        ? "quote.action_not_permitted_for_role"
        : "quote.transition_not_allowed";

    public bool BlockedByRole => blockedByRole;
}

public sealed class QuoteAlreadyAcceptedException(Guid requestId)
    : DomainException($"Request '{requestId}' already has an accepted quote.")
{
    public override string Code => "quote.already_accepted";
}

public sealed class QuoteNotEditableException(QuoteStatusName status)
    : DomainException($"A quote in state '{status.Value}' can no longer be edited.")
{
    public override string Code => "quote.not_editable";
}

public sealed class RequestNotEditableException(string reason)
    : DomainException(reason)
{
    public override string Code => "request.not_editable";
}

public sealed class RequestCreationNotPermittedException()
    : DomainException("The current user's roles do not permit creating a request.")
{
    public override string Code => "request.creation_not_permitted";
}

public sealed class OrganizationActionNotPermittedException(string action)
    : DomainException($"The current user's roles do not permit '{action}' on an organization.")
{
    public override string Code => "organization.action_not_permitted_for_role";
}

public sealed class RequestActionNotPermittedException(string action)
    : DomainException($"The current user's roles do not permit '{action}' on this request.")
{
    public override string Code => "request.action_not_permitted_for_role";
}

public sealed class QuoteConcurrencyException(Guid quoteId, int expectedVersion, int actualVersion)
    : DomainException(
        $"Quote '{quoteId}' has changed since it was read (expected version {expectedVersion}, found {actualVersion}).")
{
    public override string Code => "quote.concurrent_modification";
}

public sealed class QuoteNotFoundInRequestException(Guid requestId, Guid quoteId)
    : DomainException($"Request '{requestId}' has no quote '{quoteId}'.")
{
    public override string Code => "quote.not_found";
}

/// <summary>
/// Thrown directly from <c>UserEndpoints</c> rather than a domain aggregate method - <c>AppUser</c>
/// deliberately has no aggregate of its own (see its own doc comment), so there is nowhere else for
/// this guard to live, but the refusal still needs the same stable wire code and status mapping
/// every other permission refusal gets via <c>DomainExceptionHandler</c>.
/// </summary>
public sealed class UserActionNotPermittedException(string action)
    : DomainException($"The current user's roles do not permit '{action}' on this user.")
{
    public override string Code => "user.action_not_permitted_for_role";
}

/// <summary>
/// A self-service password change supplied a current password that doesn't match - distinct from
/// <c>auth.invalid_credentials</c> at login (which never confirms whether the email even exists);
/// here the caller is already authenticated as this exact user, so confirming their current
/// password was wrong leaks nothing new.
/// </summary>
public sealed class InvalidCurrentPasswordException()
    : DomainException("The current password is incorrect.")
{
    public override string Code => "user.invalid_current_password";
}

/// <summary>
/// Wrapper that lets exception messages name a status without <c>Domain.Common</c> depending
/// on the quote namespace, keeping the exception types usable from any aggregate.
/// </summary>
public readonly record struct QuoteStatusName(string Value);
