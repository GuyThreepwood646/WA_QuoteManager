# Security Overview

[← Back to README](../README.md) · See also: [User Roles](user-roles.md) · [API Endpoints](api.md)

A high-level tour of how the app is secured end to end. Each item links to the file or doc with
the full detail — this page is the map, not a duplicate of it.

## Authentication

Stateless JWT bearer tokens, HS256-signed with a key from configuration, issued by
`POST /api/auth/login` after verifying the submitted password against a hash created with ASP.NET
Core Identity's `PasswordHasher` (PBKDF2 under the hood — passwords are never stored or logged in
plain text). Tokens carry the user's id, display name, email, one claim per role, and their
organization id, and live 8 hours; there is no server-side session store, so revocation is
"wait for expiry," not something this app implements. A wrong email and a wrong password return
the identical `401 auth.invalid_credentials` message, so a caller can't tell "no such account" from
"wrong password" from the response body. See [`POST /api/auth/login`](api.md#post-apiauthlogin).

## Authorization

Deny-by-default: a fallback policy requires an authenticated user on *every* endpoint, and
anonymity is explicit opt-in on exactly five routes (login, `/health`, the OpenAPI document, the
Scalar reference UI, the SPA's own static files/fallback) — enforced by an integration test, not
just documentation. Role checks live in domain code (`QuoteTransitions.PermittedFor`,
`Request.Create`, `Organization.Create`, and the `UserEndpoints` handlers), not `[Authorize(Roles = "...")]`
attributes, so the API and the UI's buttons can't silently drift apart, and a vendor-owned action
additionally checks the caller's organization id against the resource's owning organization
(`DomainActor.CanActForVendorOrganization`) so a role alone is never enough to act on someone
else's data. Full detail: [User Roles → Role-based security](user-roles.md#role-based-security).

## IDOR protection: identical 404s

Two endpoints refuse a request the caller has no right to see or touch with the *same* `404` a
truly nonexistent id would get, rather than a `403` that would confirm the id is real:
`GET /api/requests/{id}/quotes/{quoteId}` for a competitor's quote, and
`PUT /api/users/{userId}` / `POST /api/users/{userId}/reset-password` for a non-admin targeting
anyone but themselves. A distinguishing status code would let a caller enumerate valid ids by
probing and watching which ones come back `403` instead of `404`.

## Password policy

Enforced identically wherever a password is set — creating a user or resetting one, self-service or
admin-on-behalf — by one shared `PasswordPolicy.Evaluate` on the backend and one shared
`PASSWORD_REQUIREMENTS` list on the frontend, so the two can't drift: minimum 8 characters, at
least one uppercase letter, one lowercase letter, one digit, and one special character. The
frontend renders this as a live red-X/green-check list while typing. Full detail:
[Users → Password requirements](api.md#users).

## Content Security Policy

Every response carries a baseline `Content-Security-Policy` header, set by a small piece of
middleware near the top of `Program.cs`. This API is the actual security boundary for the header,
not just the JSON endpoints: the same process also serves the built SPA (`UseStaticFiles` /
`MapFallbackToFile("/index.html")`), so the header lands on the HTML shell, its JS/CSS bundles, and
every `/api` response alike.

```
default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline';
img-src 'self' data:; font-src 'self'; connect-src 'self'; object-src 'none';
base-uri 'self'; form-action 'self'; frame-ancestors 'none'
```

- Everything defaults to same-origin (`'self'`); there are no third-party scripts, fonts, or
  images anywhere in the app, so nothing else needed opening up.
- `style-src` allows `'unsafe-inline'` because Radix UI (underlying shadcn/ui's `Select`/`Popover`)
  positions its portaled dropdowns with inline `style="..."` attributes — without it, the
  Organization picker on the Users and Organizations forms would render in the wrong place or not
  open at all.
- `frame-ancestors 'none'` blocks the app from being framed by another site (the CSP-native
  replacement for `X-Frame-Options: DENY`).
- **`/scalar` and `/openapi` are excluded** — Scalar's dev-only API reference page loads its own UI
  bundle from a CDN, which this policy would otherwise block. Excluding two developer-tool routes
  was judged simpler and less risky than widening `script-src` for the whole app just to
  accommodate a page that isn't part of the product surface (and isn't reachable outside
  Development in the first place).
- Enforced by `SecurityHeadersTests` (`tests/QuoteManager.Api.IntegrationTests/Security/`): the
  header is asserted present on `/`, `/health`, and an API route, and asserted absent on `/scalar`.

## No CORS, no CSRF surface

The SPA and API always share one origin — the Vite dev proxy forwards `/api`/`/health` to the
backend in development, and the API serves the built SPA directly in production — so there is no
cross-origin configuration anywhere in the app. Authentication is a bearer token read from an
`Authorization` header the SPA sets explicitly, never a cookie the browser attaches automatically,
which is what would otherwise create CSRF exposure.

## SQL injection

Every database access goes through EF Core's LINQ query provider, which parameterizes values
rather than concatenating SQL — there is no raw/interpolated SQL anywhere in the codebase (enforced
implicitly by there being no `FromSqlRaw`/`ExecuteSqlRaw` call in the project at all).

## Audit trail

Every domain-significant action (a quote drafted, submitted, accepted; a request created, awarded;
a vendor invited) is recorded as an `AuditEntry` row in the *same* database transaction as the
change itself, not derived from application logs after the fact — so the "what happened, and who
did it" trail can't silently fall out of sync with the data it describes. See
[`GET /api/requests/{requestId}/activity`](api.md#get-apirequestsrequestidactivity).

## Secrets management

`Jwt:SigningKey` is the one setting in the app that's a real secret, and the committed value is
explicitly a demo placeholder. Azure Key Vault scaffolding is wired in but optional — see
[README → Secrets: Azure Key Vault](../README.md#secrets-azure-key-vault-optional) for the full
writeup, including the honest caveat that only the "no vault configured" path is verified here,
since there's no real Azure subscription to test the "vault configured" path against.
