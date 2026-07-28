namespace QuoteManager.Domain.Common;

/// <summary>
/// A refused business operation, carrying a stable machine code.
/// </summary>
/// <remarks>
/// AD-8: the <see cref="Code"/> is what crosses the wire as the <c>code</c> extension on the
/// problem details response and is the only thing the UI is allowed to branch on. Codes are
/// therefore part of the public contract and must not be reworded casually, unlike messages.
/// </remarks>
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
/// Wrapper that lets exception messages name a status without <c>Domain.Common</c> depending
/// on the quote namespace, keeping the exception types usable from any aggregate.
/// </summary>
public readonly record struct QuoteStatusName(string Value);
