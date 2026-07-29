using QuoteManager.Domain.Common;
using QuoteManager.Domain.Identity;
using QuoteManager.Domain.Quotes;

namespace QuoteManager.Domain.Requests;

/// <summary>
/// The lifecycle of a request, which is driven entirely by its quotes.
/// </summary>
/// <remarks>
/// Deliberately small and without a transition table of its own. A request has no status endpoint;
/// <see cref="Awarded"/> is reached only as a consequence of accepting a quote, inside the same
/// transaction. Giving requests an independent lifecycle would create a second way to express the
/// same fact and therefore a second way for it to be wrong.
/// </remarks>
public enum RequestStatus
{
    Open,
    Awarded,
    Cancelled,
}

/// <summary>
/// A request for quotes, and the aggregate root that owns them.
/// </summary>
/// <remarks>
/// The root is the request rather than the quote because the invariant that matters — at most one
/// accepted quote — spans siblings. Enforcing it requires a boundary that can see all of them, so
/// every quote mutation enters through this class.
/// </remarks>
public sealed class Request : AggregateRoot
{
    private readonly List<Quote> _quotes = [];
    private readonly List<RequestInvitation> _invitations = [];

    private Request(
        Guid id,
        string title,
        string? description,
        Guid clientOrganizationId,
        DateTimeOffset? neededBy,
        DomainActor actor,
        DateTimeOffset createdAt)
        : base(id)
    {
        Title = title;
        Description = description;
        ClientOrganizationId = clientOrganizationId;
        NeededBy = neededBy;
        Status = RequestStatus.Open;
        CreatedAt = createdAt;
        Raise(new RequestCreated(id, title, clientOrganizationId, actor.Id, createdAt));
    }

    // EF Core materialisation.
    private Request()
    {
    }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public Guid ClientOrganizationId { get; private set; }

    public RequestStatus Status { get; private set; }

    public DateTimeOffset? NeededBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyList<Quote> Quotes => _quotes;

    public IReadOnlyList<RequestInvitation> Invitations => _invitations;

    public Guid? AcceptedQuoteId => _quotes.SingleOrDefault(q => q.Status == QuoteStatus.Accepted)?.Id;

    /// <summary>
    /// Invited vendors who have not yet drafted a quote.
    /// </summary>
    /// <remarks>
    /// The triage signal the invitation list exists to produce: silence is otherwise invisible,
    /// because a request with no quotes looks identical whether nobody was asked or nobody replied.
    /// </remarks>
    public IReadOnlyList<Guid> AwaitingResponseFrom =>
        [.. _invitations
            .Select(invitation => invitation.VendorOrganizationId)
            .Where(vendorId => !_quotes.Exists(quote => quote.VendorOrganizationId == vendorId))];

    public static Request Create(
        string title,
        string? description,
        Guid clientOrganizationId,
        DateTimeOffset? neededBy,
        DomainActor actor,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        // Mirrors the vendor-ownership gate on AddQuote: this is the client side of the same
        // problem. A Vendor or Reviewer account raising a request is not a harmless no-op, it's
        // one organisation's staff fabricating a request on behalf of a client they don't
        // represent - checked here, once, rather than trusted to every future caller.
        if (!actor.Roles.HasAny(AppRole.Requester | AppRole.Admin))
        {
            throw new RequestCreationNotPermittedException();
        }

        return new Request(
            Guid.CreateVersion7(now),
            title.Trim(),
            string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            clientOrganizationId,
            neededBy,
            actor,
            now);
    }

    /// <summary>
    /// Whether the request's own fields may still be changed.
    /// </summary>
    /// <remarks>
    /// The gate is the arrival of the first quote past <see cref="QuoteStatus.Draft"/>, not the
    /// request's status: once a vendor has priced something, changing the scope underneath them
    /// invalidates their offer silently.
    /// </remarks>
    public bool IsEditable =>
        Status == RequestStatus.Open && !_quotes.Exists(q => q.Status != QuoteStatus.Draft);

    public void Update(string title, string? description, DateTimeOffset? neededBy, DomainActor actor, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        if (!IsEditable)
        {
            throw new RequestNotEditableException(
                Status == RequestStatus.Open
                    ? "This request cannot be changed because vendors have already submitted quotes against it."
                    : $"A request in state '{Status}' can no longer be changed.");
        }

        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        NeededBy = neededBy;
        Raise(new RequestUpdated(Id, Title, actor.Id, now));
    }

    /// <summary>
    /// Invites a vendor organisation to quote.
    /// </summary>
    /// <remarks>
    /// Idempotent by design: inviting the same vendor twice is a harmless duplicate click, not an
    /// error worth interrupting a user over, and the composite primary key would reject the second
    /// row anyway. Returning quietly is kinder than surfacing a constraint violation.
    /// </remarks>
    public void InviteVendor(Guid vendorOrganizationId, DomainActor actor, DateTimeOffset now)
    {
        if (Status != RequestStatus.Open)
        {
            throw new RequestNotEditableException(
                $"Vendors cannot be invited to a request in state '{Status}'.");
        }

        if (_invitations.Exists(invitation => invitation.VendorOrganizationId == vendorOrganizationId))
        {
            return;
        }

        _invitations.Add(new RequestInvitation(Id, vendorOrganizationId, now));
        Raise(new VendorInvited(Id, vendorOrganizationId, actor.Id, now));
    }

    public Quote AddQuote(
        Guid vendorOrganizationId,
        Money amount,
        DateTimeOffset? expiresAt,
        string? notes,
        DomainActor actor,
        DateTimeOffset now)
    {
        if (Status != RequestStatus.Open)
        {
            throw new RequestNotEditableException(
                $"Quotes cannot be added to a request in state '{Status}'.");
        }

        // Creating a quote is the other vendor-owned gate. Checking here, not only on transitions,
        // means a Vendor can't plant a draft under a competitor's organisation and then leave it
        // for that competitor to discover.
        if (!actor.CanActForVendorOrganization(vendorOrganizationId))
        {
            throw new QuoteTransitionNotAllowedException(
                new QuoteStatusName(QuoteStatus.Draft.ToString()),
                "Create",
                blockedByRole: true);
        }

        var quote = Quote.Draft(Id, vendorOrganizationId, amount, expiresAt, notes, now);
        _quotes.Add(quote);
        Raise(new QuoteDrafted(Id, quote.Id, vendorOrganizationId, amount, actor.Id, now));
        return quote;
    }

    public void EditQuote(
        Guid quoteId,
        Money amount,
        DateTimeOffset? expiresAt,
        string? notes,
        DomainActor actor,
        DateTimeOffset now,
        int? expectedVersion = null)
    {
        var quote = RequireQuote(quoteId);
        Guard(quote, QuoteAction.Edit, actor, expectedVersion);

        quote.ApplyEdit(amount, expiresAt, notes);
        Raise(new QuoteEdited(Id, quoteId, amount, actor.Id, now));
    }

    /// <summary>
    /// The single entry point for every quote status change.
    /// </summary>
    /// <remarks>
    /// Legality and role permission are both resolved by <see cref="QuoteTransitions"/>, so this
    /// method contains no <c>switch</c> over status and no role literal. The only behaviour that
    /// lives here is the consequence of a transition, which is what actually needs the aggregate.
    /// </remarks>
    public void ApplyQuoteAction(
        Guid quoteId,
        QuoteAction action,
        DomainActor actor,
        DateTimeOffset now,
        int? expectedVersion = null)
    {
        if (action == QuoteAction.Edit)
        {
            throw new ArgumentException(
                $"Use {nameof(EditQuote)} for field changes; {nameof(QuoteAction.Edit)} is not a status transition.",
                nameof(action));
        }

        var quote = RequireQuote(quoteId);
        var resulting = Guard(quote, action, actor, expectedVersion);

        if (action == QuoteAction.Accept && _quotes.Exists(q => q.Status == QuoteStatus.Accepted))
        {
            throw new QuoteAlreadyAcceptedException(Id);
        }

        var from = quote.Status;
        quote.ApplyStatus(resulting, now);
        Raise(new QuoteStatusChanged(Id, quoteId, action, from, resulting, null, actor.Id, now));

        if (action != QuoteAction.Accept)
        {
            return;
        }

        // Accepting is the one action with consequences beyond its own quote. Rejecting the live
        // siblings and awarding the request happen here rather than in a handler so that they
        // cannot be omitted by a caller and cannot land in a separate transaction.
        const string superseded = "SupersededByAcceptedQuote";

        foreach (var sibling in _quotes)
        {
            if (sibling.Id == quoteId || sibling.Status is not (QuoteStatus.Submitted or QuoteStatus.UnderReview))
            {
                continue;
            }

            var siblingFrom = sibling.Status;
            sibling.ApplyStatus(QuoteStatus.Rejected, now, superseded);
            Raise(new QuoteStatusChanged(
                Id,
                sibling.Id,
                QuoteAction.Reject,
                siblingFrom,
                QuoteStatus.Rejected,
                superseded,
                actor.Id,
                now));
        }

        Status = RequestStatus.Awarded;
        Raise(new RequestAwarded(Id, quoteId, actor.Id, now));
    }

    public void Cancel(DomainActor actor, DateTimeOffset now)
    {
        if (Status == RequestStatus.Awarded)
        {
            throw new RequestNotEditableException("An awarded request cannot be cancelled.");
        }

        if (Status == RequestStatus.Cancelled)
        {
            return;
        }

        Status = RequestStatus.Cancelled;
        Raise(new RequestCancelled(Id, actor.Id, now));
    }

    private Quote RequireQuote(Guid quoteId) =>
        _quotes.Find(q => q.Id == quoteId) ?? throw new QuoteNotFoundInRequestException(Id, quoteId);

    /// <summary>
    /// Resolves an attempted action against the transition table and the caller's expected version.
    /// </summary>
    private static QuoteStatus Guard(Quote quote, QuoteAction action, DomainActor actor, int? expectedVersion)
    {
        if (expectedVersion is { } expected && expected != quote.Version)
        {
            throw new QuoteConcurrencyException(quote.Id, expected, quote.Version);
        }

        var resolution = QuoteTransitions.Resolve(quote.Status, action, actor, quote.VendorOrganizationId);

        return resolution.IsAllowed
            ? resolution.Resulting
            : throw new QuoteTransitionNotAllowedException(
                new QuoteStatusName(quote.Status.ToString()),
                action.ToString(),
                resolution.IsDeniedByRole);
    }
}
