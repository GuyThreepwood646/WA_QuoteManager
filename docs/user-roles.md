# User Roles

[← Back to README](../README.md)

Roles are a closed set of four, stored as flags on each user account (`AppUser.Roles`) and issued
as JWT claims at login — a user can hold more than one simultaneously, though the seeded demo
accounts each hold exactly one. Every write endpoint is authorized by **domain logic reading these
claims**, not by a role attribute on the endpoint — see [Role-based security](#role-based-security)
below for why, and how that's wired.


| Role          | What this user does                                                                                                                                                                                                                                            | Organization scoping                                                                                                                                                                                                                                                                      |
| ------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Admin**     | Platform staff. Can do everything any other role can do, across every organization — create requests on behalf of any client, draft or transition a quote for any vendor, force-expire a stale quote. The one role with no organization-ownership restriction. | None (acts as any org)                                                                                                                                                                                                                                                                    |
| **Requester** | Represents a *client* company that needs storage/packing/transport. Raises new requests (`POST /api/requests`) describing what's needed and by when.                                                                                                           | Tied to one client organization, but read access is not filtered (see below)                                                                                                                                                                                                              |
| **Reviewer**  | Represents the client side evaluating offers. Moves a submitted quote into review, and decides its outcome — `StartReview`, `Accept`, `Reject`, `ReturnToSubmitted`. Cannot draft or withdraw a quote (that's the vendor's action) and cannot create requests. | Not organization-scoped — a Reviewer can review any request's quotes                                                                                                                                                                                                                      |
| **Vendor**    | Represents a storage facility, packing crew, or freight carrier responding to a request. Drafts a quote (`POST .../quotes`), then `Submit`s or `Withdraw`s it.                                                                                                 | **Strictly scoped to its own organization** — a Vendor account cannot draft, submit, or withdraw a quote belonging to a different vendor organization, and (unless also Admin/Reviewer/Requester) cannot even *read* another vendor's quote details, notes, or amount on a shared request |


## Role-based security

- **Authentication**: stateless JWT bearer tokens, HS256-signed with a key from configuration,
issued by `POST /api/auth/login` after verifying a password hash created with ASP.NET Core
Identity's `PasswordHasher`. Tokens live 8 hours.
- **Roles travel on the token**: one `role` claim per role the user holds, plus the user's id,
display name, email, and (if applicable) their organization id — all read back per-request via
`ICurrentUser` (`Infrastructure/Identity/CurrentUser.cs`), which wraps the authenticated
`HttpContext.User` and is the *only* place role/identity data enters application code.
- **Deny-by-default**: a fallback authorization policy requires an authenticated user on *every*
endpoint. Anonymity is explicit opt-in (`.AllowAnonymous()`) on exactly five routes: login,
`/health`, the OpenAPI document, the Scalar reference UI, and the SPA's own static
files/fallback route — enforced by an integration test, not just documentation.
- **Authorization lives in domain code, not endpoint attributes**: rather than `[Authorize(Roles = "...")]` on each route (which would let the API and the UI's displayed buttons silently drift
apart), every write path resolves permission through one function,
`QuoteTransitions.PermittedFor` / `.Resolve`, that the same endpoint uses both to authorize the
attempt *and* to tell the client which buttons to show. A vendor-owned action additionally checks
the caller's organization id against the quote's vendor organization id
(`DomainActor.CanActForVendorOrganization`) — so a Vendor role alone isn't enough to act on
*someone else's* quote.

For the wider security picture — CSP, IDOR protections, password policy, secrets management — see
[Security Overview](security.md).
