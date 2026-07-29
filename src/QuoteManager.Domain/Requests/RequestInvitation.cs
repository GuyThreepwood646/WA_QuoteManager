namespace QuoteManager.Domain.Requests;

/// <summary>
/// A vendor organization invited to quote on a request: the genuine many-to-many in the model,
/// kept separate from the request's own (one-to-many) client organization. Exists for triage —
/// without it, silence from an invited vendor is indistinguishable from never having been asked.
/// </summary>
public sealed class RequestInvitation
{
    internal RequestInvitation(Guid requestId, Guid vendorOrganizationId, DateTimeOffset invitedAt)
    {
        RequestId = requestId;
        VendorOrganizationId = vendorOrganizationId;
        InvitedAt = invitedAt;
    }

    // EF Core materialisation.
    private RequestInvitation()
    {
    }

    public Guid RequestId { get; private set; }

    public Guid VendorOrganizationId { get; private set; }

    public DateTimeOffset InvitedAt { get; private set; }
}
