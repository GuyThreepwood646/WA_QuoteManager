# Warehouse Anywhere — Quote Manager

A service-request and quote management tool modelled on Warehouse Anywhere's actual business:
client companies that need somewhere to store goods (and get them packed and moved) submit a
**request**, and a partner network of storage facilities, packing vendors, and carriers respond
with **quotes**. A reviewer takes each quote through a review lifecycle and accepts exactly one
per request.

Stack: ASP.NET Core 10 (minimal APIs) + EF Core/SQLite on the backend, React 19 + Vite + Tailwind
CSS v4 + shadcn/ui on the frontend, JWT bearer authentication, and a modular-monolith/ports-and-
adapters architecture (Domain → Application → Infrastructure/Api, dependencies only pointing
inward).

---

## 1. Project Setup

### Prerequisites

| Tool | Version | Notes |
| --- | --- | --- |
| .NET SDK | 10.0.100+ | Pinned via `global.json` (`rollForward: latestMinor`), so any 10.x SDK works. |
| Node.js | 22.x | `react-router` 7.18.1 is used specifically because it has no Node-version floor above 20; any current Node 22 works. |
| npm | bundled with Node | No separate install needed. |

No database server, Docker, or cloud account is required. Persistence is a single SQLite file
created automatically on first run; Azure integrations (Application Insights, Service Bus) are
present in the code but are **configuration-gated** — absent config, they're simply not used.

### Clone and restore

```bash
git clone <repo-url>
cd WA_QuoteManager
dotnet restore
dotnet tool restore
```

`dotnet tool restore` installs the local `dotnet-ef` CLI tool (pinned in
`.config/dotnet-tools.json`). You only need it if you intend to add a new EF Core migration by
hand — the app applies existing migrations itself at startup.

### Run it — fastest path (single process, one command)

```bash
dotnet run -c Release --project src/QuoteManager.Api
```

This one command:
1. Runs `npm ci && npm run build` for the React app (an MSBuild target wired into
   `QuoteManager.Api.csproj`, Release-only) and copies the built assets into `wwwroot`.
2. Applies any pending EF Core migrations to a local `quotemanager.db` SQLite file (created if it
   doesn't exist).
3. Seeds demo data on an empty database (idempotent — running again is a no-op once data exists).
4. Serves the API and the SPA from the same origin: **http://localhost:5080**.

### Run it — development mode (hot reload)

Two terminals:

```bash
# Terminal 1 — API on http://localhost:5080
dotnet run --project src/QuoteManager.Api
```

```bash
# Terminal 2 — Vite dev server on http://localhost:5173, proxies /api and /health to :5080
cd src/QuoteManager.Web
npm install
npm run dev
```

Open **http://localhost:5173**. The browser only ever talks to one origin; Vite's dev proxy
forwards API calls to the backend, so there's no CORS configuration anywhere in the app.

### Demo credentials

The seeded database includes one account per role, all sharing the password below:

| Email | Role | Organization |
| --- | --- | --- |
| `admin@warehouseanywhere.test` | Admin | — (platform staff) |
| `requester@warehouseanywhere.test` | Requester | Meridian Pharma Sampling (client) |
| `reviewer@warehouseanywhere.test` | Reviewer | Palmetto Retail & CPG (client) |
| `vendor@warehouseanywhere.test` | Vendor | SecureBase Self Storage |
| `vendor2@warehouseanywhere.test` | Vendor | Crateworks Packing & Crating |
| `vendor3@warehouseanywhere.test` | Vendor | Interstate Freight Partners |

**Password (all accounts):** `Demo!2345`

This password is deliberately not secret — it's published here on purpose, and the seed hashes it
the same way a real signup would.

### Configuration

Defaults live in `src/QuoteManager.Api/appsettings.json` and need no changes to run locally:

| Setting | Default | Purpose |
| --- | --- | --- |
| `ConnectionStrings:QuoteManager` | `Data Source=quotemanager.db` | SQLite file path (gitignored, created on first run). |
| `Jwt:SigningKey` | a committed demo key | HS256 signing key for bearer tokens. **Not fit for production** — swap it via environment variable / user secrets before deploying anywhere real. |
| `Jwt:Issuer` / `Jwt:Audience` | `QuoteManager` / `QuoteManager.Client` | Token validation parameters. |
| `AzureMonitor:ConnectionString` | unset | If present, OpenTelemetry exports to Azure Monitor. Absent by default — telemetry still works locally via the console/OTel pipeline. |
| `ServiceBus:ConnectionString` | unset | If present, integration events publish to Azure Service Bus. Absent by default — an in-process channel adapter is used instead, so outbox/messaging works with zero cloud setup. |

### Running the tests

```bash
dotnet build -warnaserror   # matches CI: zero warnings tolerated
dotnet test                 # 184 tests: Domain, Architecture (dependency-direction rules), Infrastructure, API integration
```

Frontend:

```bash
cd src/QuoteManager.Web
npm run build   # tsc -b && vite build
npm run lint    # oxlint
```

End-to-end (Playwright, spins up both the real API and the Vite dev server itself):

```bash
cd tests/QuoteManager.Web.E2ETests
npm install
npx playwright install chromium   # first time only
npx playwright test
```

---

## 2. User Roles

Roles are a closed set of four, stored as flags on each user account (`AppUser.Roles`) and issued
as JWT claims at login — a user can hold more than one simultaneously, though the seeded demo
accounts each hold exactly one. Every write endpoint is authorized by **domain logic reading these
claims**, not by a role attribute on the endpoint — see [Role-based security](#role-based-security)
below for why, and how that's wired.

| Role | What this user does | Organization scoping |
| --- | --- | --- |
| **Admin** | Platform staff. Can do everything any other role can do, across every organization — create requests on behalf of any client, draft or transition a quote for any vendor, force-expire a stale quote. Also the only role that can create, rename, or retire organizations. The one role with no organization-ownership restriction. | None (acts as any org) |
| **Requester** | Represents a *client* company that needs storage/packing/transport. Raises new requests (`POST /api/requests`), edits or cancels them while still open, and invites vendor organizations to quote. | Tied to one client organization, but read access is not filtered (see below) |
| **Reviewer** | Represents the client side evaluating offers. Moves a submitted quote into review, and decides its outcome — `StartReview`, `Accept`, `Reject`, `ReturnToSubmitted`. Cannot draft, edit, or withdraw a quote (that's the vendor's action) and cannot create, edit, cancel, or invite vendors to a request. | Not organization-scoped — a Reviewer can review any request's quotes |
| **Vendor** | Represents a storage facility, packing crew, or freight carrier responding to a request. Drafts a quote (`POST .../quotes`), edits its business fields while still `Draft`, then `Submit`s or `Withdraw`s it. | **Strictly scoped to its own organization** — a Vendor account cannot draft, edit, submit, or withdraw a quote belonging to a different vendor organization, and (unless also Admin/Reviewer/Requester) cannot even *read* another vendor's quote details, notes, or amount on a shared request |

### Role-based security

Yes — this is fully implemented, and entirely local (no Azure AD, no external identity provider,
no paid service of any kind):

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
- **Authorization lives in domain code, not endpoint attributes**: rather than `[Authorize(Roles =
  "...")]` on each route (which would let the API and the UI's displayed buttons silently drift
  apart), every write path resolves permission through one function,
  `QuoteTransitions.PermittedFor` / `.Resolve`, that the same endpoint uses both to authorize the
  attempt *and* to tell the client which buttons to show. A vendor-owned action additionally checks
  the caller's organization id against the quote's vendor organization id
  (`DomainActor.CanActForVendorOrganization`) — so a Vendor role alone isn't enough to act on
  *someone else's* quote.

---

## 3. API Endpoints

All endpoints are under `/api` and (except login) require `Authorization: Bearer <token>`. Every
error response is an [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) Problem Details object:

```jsonc
{ "type": "...", "title": "...", "status": 409, "detail": "...", "code": "quote.transition_not_allowed", "traceId": "..." }
```

`code` is the one stable, machine-readable field a client should branch on — `detail` is
human-readable prose and may be reworded. Validation failures (malformed request bodies) instead
return the standard ASP.NET Core `ValidationProblemDetails` shape:

```jsonc
{ "type": "...", "title": "One or more validation errors occurred.", "status": 400, "errors": { "Title": ["The Title field is required."] } }
```

List endpoints always return the same envelope, never a bare array:

```jsonc
{ "items": [ ... ], "page": 1, "pageSize": 25, "total": 6 }
```

— accepting `?page=` (1-based) and `?pageSize=` (default 25, clamped to a max of 100) as query
parameters; an explicit `page=0` or negative `pageSize` is rejected as a validation error rather
than silently clamped.

### Auth

#### `POST /api/auth/login`

**Auth:** anonymous.

**JSON input:**

| Field | Type | Validation |
| --- | --- | --- |
| `email` | `string` | Required, must be a syntactically valid email address, no leading/trailing whitespace |
| `password` | `string` | Required (non-empty) |

**JSON output — `200 OK`:**

```jsonc
{
  "accessToken": "eyJ...",
  "expiresAt": "2026-07-28T20:00:00Z",
  "user": { "id": "guid", "displayName": "Ada Admin", "roles": ["Admin"], "organizationId": null }
}
```

**Errors:** `400` (validation — malformed email/blank password); `401` with `code:
"auth.invalid_credentials"` for an unknown email or wrong password (the same generic message
either way, so a caller can't enumerate valid emails).

**Business logic:** looks up the user by email, verifies the submitted password against the
stored hash via `PasswordHasher.VerifyHashedPassword`. On success, issues a JWT carrying the
user's id, display name, email, one claim per role, and their organization id (if any), valid for
8 hours. No session state is kept server-side — the token *is* the session.

#### `GET /api/auth/me`

**Auth:** any authenticated user.

**JSON input:** none.

**JSON output — `200 OK`:** same `user` shape as login's response:

```jsonc
{ "id": "guid", "displayName": "Ada Admin", "roles": ["Admin"], "organizationId": null }
```

**Business logic:** reads the claims already present on the bearer token via `ICurrentUser` and
echoes them back. Exists so the SPA can rehydrate "who am I" after a page refresh without
decoding the JWT client-side, and doubles as the anonymous-set test's one protected route.

### Dashboard

#### `GET /api/dashboard`

**Auth:** any authenticated user (every role sees the same triage data; only each quote's
`permittedActions` — see below — differs by role/organization).

**JSON input:** none.

**JSON output — `200 OK`:**

```jsonc
{
  "quotesNeedingReview": [ /* QuoteTriageItem */ ],
  "quotesUnderReview": [ /* QuoteTriageItem */ ],
  "quotesExpiringSoon": [ /* QuoteTriageItem */ ],
  "requestsAwaitingResponse": [ /* RequestAwaitingResponseItem */ ]
}
```

`QuoteTriageItem`:

```jsonc
{
  "quoteId": "guid",
  "requestId": "guid",
  "requestTitle": "string",
  "vendorOrganizationName": "string",
  "amount": 0.00,
  "currency": "string",
  "status": "string",
  "expiresAt": "datetime|null",
  "statusChangedAt": "datetime",
  "version": 0,
  "permittedActions": ["string"]
}
```

`RequestAwaitingResponseItem`:

```jsonc
{
  "requestId": "guid",
  "title": "string",
  "clientOrganizationName": "string",
  "createdAt": "datetime",
  "awaitingVendorNames": ["string"]
}
```

**Business logic:** this is a pure read model — a single-purpose triage surface, not a
filtered view of one big list, built from four independent projections over the same
`Quotes`/`Requests`/`Organizations`/`RequestInvitations` tables:
- **Needs your review** — every quote in `Submitted` status (oldest first).
- **Under review** — every quote in `UnderReview` status.
- **Expiring soon** — any `Submitted`/`UnderReview` quote whose `expiresAt` falls within 3 days of
  now (using the injected `TimeProvider`, never wall-clock time, so this is deterministic under
  test).
- **Awaiting vendor response** — open requests where at least one invited vendor organization has
  not yet drafted *any* quote; each entry lists every silent vendor by name. A request that's been
  awarded or cancelled never appears here even if a vendor never replied.

Each quote's `permittedActions` is computed the same way the quote endpoints compute it (see
below) for the *calling* user — so two different users hitting this same endpoint see the same
quotes, but different action buttons.

### Requests

#### `GET /api/requests`

**Auth:** any authenticated user.

**JSON input:** query string only — `page`, `pageSize` (see paging conventions above).

**JSON output — `200 OK`:** paged envelope of `RequestListItem`:

```jsonc
{
  "id": "guid",
  "title": "string",
  "clientOrganizationName": "string",
  "status": "Open|Awarded|Cancelled",
  "quoteCount": 0,
  "neededBy": "datetime|null",
  "createdAt": "datetime"
}
```

**Business logic:** a thin, unfiltered list (every request, newest first) — deliberately carrying
no fields the list screen doesn't use. Read access is not organization-scoped at this level; the
detail endpoint below is where per-quote visibility actually narrows.

#### `POST /api/requests`

**Auth:** any authenticated user, but only succeeds for **Requester** or **Admin** (enforced in
the domain, see below).

**JSON input:**

| Field | Type | Validation |
| --- | --- | --- |
| `title` | `string` | Required, 1–200 characters |
| `description` | `string?` | Optional, max 2000 characters |
| `clientOrganizationId` | `guid` | Required (rejected as a validation error if `Guid.Empty`) |
| `neededBy` | `datetime?` | Optional |

**JSON output — `201 Created`** (`Location: /api/requests/{id}`): a full `RequestDetailResponse`
(see the `GET` detail endpoint below) for the newly created request — `status: "Open"`, no quotes
or invitations yet.

**Errors:**
- `400` validation problem — blank/oversized title, empty `clientOrganizationId`.
- `400` with `code: "request.unknown_client_organization"` — `clientOrganizationId` doesn't
  reference an existing organization, or references one that isn't a **client** organization (e.g.
  a vendor's id was passed by mistake).
- `403` with `code: "request.creation_not_permitted"` — caller is authenticated but holds neither
  Requester nor Admin.

**Business logic:** looks up the named client organization first (so a bad id is a clear 400
naming the field, not a confusing downstream failure), then calls `Request.Create`, which is the
*sole* authority on whether the caller's role permits raising a request — the endpoint itself
contains no role check, only the domain does, so there is exactly one place this rule can drift
from what the UI expects.

#### `GET /api/requests/{requestId}`

**Auth:** any authenticated user.

**JSON input:** `requestId` (route, `guid`).

**JSON output — `200 OK`:**

```jsonc
{
  "id": "guid",
  "title": "string",
  "description": "string|null",
  "clientOrganizationId": "guid",
  "clientOrganizationName": "string",
  "status": "Open|Awarded|Cancelled",
  "neededBy": "datetime|null",
  "createdAt": "datetime",
  "isEditable": true,
  "canAddQuote": true,
  "canEdit": true,
  "canCancel": true,
  "canInviteVendor": true,
  "quotes": [ /* RequestQuoteItem */ ],
  "invitations": [ /* RequestInvitationItem */ ]
}
```

`RequestQuoteItem`:

```jsonc
{
  "id": "guid",
  "vendorOrganizationId": "guid",
  "vendorOrganizationName": "string",
  "status": "string",
  "amount": 0.00,
  "currency": "string",
  "expiresAt": "datetime|null",
  "notes": "string|null",
  "statusChangedAt": "datetime",
  "statusReason": "string|null",
  "version": 0,
  "permittedActions": ["string"]
}
```

`RequestInvitationItem`:

```jsonc
{
  "vendorOrganizationId": "guid",
  "vendorOrganizationName": "string",
  "invitedAt": "datetime",
  "hasQuoted": false
}
```

**Errors:** `404` if `requestId` doesn't exist.

**Business logic:** this is the one endpoint whose response shape depends on *who's asking*:
- **Non-vendor viewers** (Admin, Reviewer, Requester) see every quote and every invitation on the
  request — they're the client side of the deal and need to compare all competing offers.
- **A pure Vendor viewer** (Vendor role and nothing else) sees **only their own organization's**
  quote and invitation entry — a competitor's amount, notes, status, or even the bare fact that a
  competing quote exists is filtered out entirely, not just hidden by the UI. `hasQuoted` on their
  own invitation is still computed against the full quote list, since a vendor is entitled to know
  whether *they* quoted regardless of who else did.
- `isEditable` mirrors `Request.IsEditable` — true only while the request is `Open` and no quote on
  it has progressed past `Draft` (once a vendor has priced something, silently changing the scope
  underneath them isn't allowed).
- `canAddQuote` is the request-level counterpart to a quote's `permittedActions`: true only when
  the request is editable, the caller holds the Vendor role, has an organization id, and that
  organization hasn't already quoted — i.e., whether *this* viewer should be shown a "draft a
  quote" form. (An Admin can still call the create-quote endpoint on behalf of any vendor as a
  support action; that's not surfaced as a screen, so it doesn't set this flag.)
- Every quote's `permittedActions` is computed the same way as the dedicated quote endpoint below.
- `canEdit`, `canCancel`, and `canInviteVendor` are the request-level equivalent of `canAddQuote`
  for the three endpoints below — each true only when the caller holds Requester or Admin, so the
  UI never has to re-derive that role check itself. `canEdit` additionally requires `isEditable`;
  `canCancel` and `canInviteVendor` require the request still be `Open`.

#### `PUT /api/requests/{requestId}`

Edits a request's own fields (title, description, needed-by). `clientOrganizationId` is absent —
it isn't editable once a request exists.

**Auth:** any authenticated user, but only succeeds for **Requester** or **Admin**, and only while
`isEditable` is true (see above).

**JSON input:**

| Field | Type | Validation |
| --- | --- | --- |
| `title` | `string` | Required, 1–200 characters |
| `description` | `string?` | Optional, max 2000 characters |
| `neededBy` | `datetime?` | Optional |

**JSON output — `200 OK`:** the updated `RequestDetailResponse`.

**Errors:**
- `400` validation problem — blank/oversized title.
- `403` with `code: "request.action_not_permitted_for_role"` — caller isn't Requester or Admin.
- `404` — request not found.
- `409` with `code: "request.not_editable"` — a quote has already progressed past `Draft`, or the
  request is `Awarded`/`Cancelled`.

#### `POST /api/requests/{requestId}/cancel`

Cancels a request. Only legal while `Open` — an awarded request represents a real commitment and
can't be called off this way.

**Auth:** any authenticated user, but only succeeds for **Requester** or **Admin**.

**JSON input:** none.

**JSON output — `200 OK`:** the updated `RequestDetailResponse`, with `status: "Cancelled"`.

**Errors:**
- `403` with `code: "request.action_not_permitted_for_role"` — caller isn't Requester or Admin.
- `404` — request not found.
- `409` with `code: "request.not_editable"` — the request is already `Awarded`.

**Business logic:** cancelling an already-cancelled request is a no-op, not an error — the same
idempotent shape `InviteVendor` (below) uses for a duplicate invite.

#### `POST /api/requests/{requestId}/invitations`

Invites a vendor organization to quote on a request.

**Auth:** any authenticated user, but only succeeds for **Requester** or **Admin**, and only while
the request is `Open`.

**JSON input:**

| Field | Type | Validation |
| --- | --- | --- |
| `vendorOrganizationId` | `guid` | Required, must not be `Guid.Empty` |

**JSON output — `200 OK`:** the updated `RequestDetailResponse`, with the new invitation reflected
in `invitations`.

**Errors:**
- `400` validation problem — empty `vendorOrganizationId`.
- `400` with `code: "request.unknown_vendor_organization"` — `vendorOrganizationId` doesn't
  reference an existing organization, or references one that isn't **vendor**-kind.
- `403` with `code: "request.action_not_permitted_for_role"` — caller isn't Requester or Admin.
- `404` — request not found.
- `409` with `code: "request.not_editable"` — the request is no longer `Open`.

**Business logic:** inviting the same vendor twice is a harmless no-op, not an error — a duplicate
click shouldn't interrupt the user.

#### `GET /api/requests/{requestId}/activity`

**Auth:** any authenticated user.

**JSON input:** `requestId` (route, `guid`); `page`, `pageSize` (query).

**JSON output — `200 OK`:** paged envelope of `ActivityEntryResponse`:

```jsonc
{
  "id": "guid",
  "subjectType": "Request|Quote",
  "subjectId": "guid",
  "action": "QuoteAccepted",
  "summary": "string",
  "actorDisplayName": "string",
  "occurredAt": "datetime"
}
```

Newest first, ties broken by id (which sorts chronologically since it's a UUIDv7).

**Errors:** `404` if `requestId` doesn't exist.

**Business logic:** a per-request "what happened" timeline, read directly from the
`AuditEntry` table — a transactional projection of domain events written in the *same* database
transaction as the change that raised them (never from application logs, which are a diagnostics
concern, not the audit source of truth). Applies the identical visibility rule as the request
detail endpoint: a pure Vendor viewer only sees `Quote`-subject rows for their own organization's
quote; every `Request`-subject row (created, vendor invited, awarded, cancelled) names no vendor
and carries no money, so it's visible to anyone who can see the request at all.

### Quotes

#### `POST /api/requests/{requestId}/quotes`

**Auth:** any authenticated user, but only succeeds when the caller may act for the named vendor
organization (see business logic).

**JSON input:** `requestId` (route, `guid`); body:

| Field | Type | Validation |
| --- | --- | --- |
| `vendorOrganizationId` | `guid` | Required, must not be `Guid.Empty` |
| `amount` | `decimal` | Required, must be greater than zero |
| `currency` | `string` | Required, exactly 3 characters (ISO-4217 code) |
| `expiresAt` | `datetime?` | Optional |
| `notes` | `string?` | Optional, max 2000 characters |

**JSON output — `201 Created`** (`Location: .../quotes/{id}`, `ETag` header carrying the quote's
version): a `QuoteResponse`:

```jsonc
{
  "id": "guid",
  "requestId": "guid",
  "vendorOrganizationId": "guid",
  "status": "Draft",
  "amount": 0.00,
  "currency": "string",
  "expiresAt": "datetime|null",
  "notes": "string|null",
  "createdAt": "datetime",
  "statusChangedAt": "datetime",
  "statusReason": null,
  "version": 0,
  "permittedActions": ["string"]
}
```

**Errors:** `400` validation problem (bad shape above); `404` if `requestId` doesn't exist; `409`
with `code: "request.not_editable"` if the request isn't `Open`; `403` with `code:
"quote.action_not_permitted_for_role"` if the caller can't act for `vendorOrganizationId`.

**Business logic:** `Request.AddQuote` is the sole authority here. It checks the request is still
`Open`, then checks `actor.CanActForVendorOrganization(vendorOrganizationId)` — true for Admin
unconditionally, true for a Vendor only when their own organization id matches the id in the
request body. This is what stops a vendor account from drafting a quote *under a competitor's
name* and leaving it for that competitor to discover. The new quote starts in `Draft`.

#### `GET /api/requests/{requestId}/quotes/{quoteId}`

**Auth:** any authenticated user.

**JSON input:** `requestId`, `quoteId` (route, `guid`).

**JSON output — `200 OK`** (with a weak `ETag` response header equal to the quote's `version`):
`QuoteResponse` — same shape as the create-quote response above.

**Errors:** `404` if the request or the quote within it doesn't exist.

**Business logic:** a straight read, projecting the aggregate's current state plus
`permittedActions` computed for the calling actor. The `ETag` is what a client round-trips as
`If-Match` on the transition call below — this endpoint is how you'd fetch a fresh version before
attempting an action.

#### `POST /api/requests/{requestId}/quotes/{quoteId}/transitions`

The one action-driven lifecycle endpoint — there is no per-status verb endpoint family
(`/accept`, `/reject`, etc.), by design.

**Auth:** any authenticated user; whether the specific action succeeds depends entirely on role
(and, for vendor-side actions, organization ownership) — see the table below.

**Required header:** `If-Match: "<version>"` — the quote's current `version`, as returned by the
`GET` endpoint or the previous transition's response. Missing or malformed → `400` with `code:
"quote.if_match_required"`, checked before any domain logic runs.

**JSON input — body:**

| Field | Type | Validation |
| --- | --- | --- |
| `action` | `string` (enum) | Required; must be one of `Submit`, `StartReview`, `ReturnToSubmitted`, `Accept`, `Reject`, `Withdraw`, `Expire` (`Edit` is a recognised enum value but is rejected — see business logic) |

**JSON output — `200 OK`** (fresh `ETag` header): the updated `QuoteResponse`.

**Errors:**
- `400` validation problem — `action` isn't a recognised value.
- `404` — request or quote not found.
- `409` with `code: "quote.if_match_required"` — missing/unparseable `If-Match`.
- `409` with `code: "quote.concurrent_modification"` — the `If-Match` version doesn't match the
  quote's current version (someone else changed it first), or a raw EF concurrency conflict.
- `409` with `code: "quote.transition_not_allowed"` — the action isn't legal from the quote's
  *current* status for anyone (e.g. `Accept` on a `Submitted` quote — only `UnderReview` quotes can
  be accepted).
- `403` with `code: "quote.action_not_permitted_for_role"` — the action would be legal from this
  status, but not for this caller (wrong role, or a Vendor acting on another organization's quote).
- `409` with `code: "quote.already_accepted"` — attempting to `Accept` a quote on a request that
  already has an accepted quote (defence in depth; the state machine below normally prevents
  reaching this).

**Business logic** — the complete lifecycle, expressed as one table
(`QuoteTransitions`, `Domain/Quotes/QuoteTransitions.cs`) that both this endpoint and every
`permittedActions` projection read from, so the UI can never offer a button the API would refuse:

| From | Action | To | Who |
| --- | --- | --- | --- |
| Draft | `Submit` | Submitted | Vendor (own org) / Admin |
| Draft | `Withdraw` | Withdrawn | Vendor (own org) / Admin |
| Submitted | `StartReview` | UnderReview | Reviewer / Admin |
| Submitted | `Withdraw` | Withdrawn | Vendor (own org) / Admin |
| Submitted | `Expire` | Expired | Admin only |
| UnderReview | `Accept` | Accepted | Reviewer / Admin |
| UnderReview | `Reject` | Rejected | Reviewer / Admin |
| UnderReview | `ReturnToSubmitted` | Submitted | Reviewer / Admin |
| UnderReview | `Expire` | Expired | Admin only |

Terminal states (no action legal from any of these, for anyone): `Accepted`, `Rejected`,
`Withdrawn`, `Expired`.

`Edit` (business-field changes to an already-drafted quote) travels in `permittedActions` as a
signal for the UI, but is **not** a status transition — sending it to this endpoint is rejected
before the transition table is even consulted, since it's a different kind of change with its own
endpoint (see `PUT` below).

**Accepting a quote has side effects beyond itself**, all inside the same database transaction:
every sibling quote on the same request still in `Submitted` or `UnderReview` is automatically
rejected (`statusReason: "SupersededByAcceptedQuote"`), and the parent request's status flips to
`Awarded`. This is what guarantees at most one accepted quote per request — enforced twice, once
here in the aggregate and again as a database-level filtered unique index, so even a race between
two concurrent accept attempts can't leave two accepted quotes.

#### `PUT /api/requests/{requestId}/quotes/{quoteId}`

Edits a quote's business fields (amount, currency, expiry, notes) — distinct from the status
transitions above, and only legal while the quote is still `Draft`.

**Auth:** any authenticated user; succeeds only for the quote's own vendor organization or an
Admin, and only while the quote is `Draft` — the identical `QuoteTransitions` check the
transition endpoint uses, resolved for the `Edit` action.

**Required header:** `If-Match: "<version>"`, identical to the transitions endpoint — missing or
malformed → `400` with `code: "quote.if_match_required"`.

**JSON input — body:**

| Field | Type | Validation |
| --- | --- | --- |
| `amount` | `decimal` | Required, must be greater than zero |
| `currency` | `string` | Required, exactly 3 characters (ISO-4217 code) |
| `expiresAt` | `datetime?` | Optional |
| `notes` | `string?` | Optional, max 2000 characters |

**JSON output — `200 OK`** (fresh `ETag` header): the updated `QuoteResponse` — same shape as the
create-quote response above.

**Errors:**
- `400` validation problem — bad shape above.
- `404` — request or quote not found.
- `409` with `code: "quote.if_match_required"` / `"quote.concurrent_modification"` — same meaning
  as the transitions endpoint.
- `409` with `code: "quote.transition_not_allowed"` — the quote has progressed past `Draft`.
- `403` with `code: "quote.action_not_permitted_for_role"` — caller isn't the owning vendor or an
  Admin.

**Business logic:** `Request.EditQuote` resolves `QuoteAction.Edit` through the same
`QuoteTransitions` table the status endpoint uses, so ownership and the Draft-only rule can never
drift between the two — there is no bespoke permission check written for this endpoint.

Every transition, not just `Accept`, is concurrency-checked via the `If-Match`/`version` pair —
two reviewers racing to decide the same quote will have the second one rejected with `409
quote.concurrent_modification` rather than silently overwriting the first.

### Organizations

#### `GET /api/organizations`

**Auth:** any authenticated user.

**JSON input:** query string — `page`, `pageSize`, `includeRetired` (`bool`, default `false`).

**JSON output — `200 OK`:** paged envelope of `OrganizationListItem`:

```jsonc
{ "id": "guid", "name": "string", "kind": "Client|Vendor", "retiredAt": "datetime|null" }
```

**Business logic:** a simple directory — every authenticated user reads every organization
(there's no per-user visibility rule here; the rule that matters, vendor-quote visibility, lives
on the requests/quotes endpoints above). Retired organizations are excluded unless
`includeRetired=true` is passed — every picker context (new-request's client dropdown,
invite-vendor's vendor dropdown) uses the default, so a retired organization is never offered for
a *new* association, while the Admin-facing Organizations screen passes `includeRetired=true` so
retired rows stay visible and manageable.

#### `POST /api/organizations`

**Auth:** any authenticated user, but only succeeds for **Admin** — every other role already acts
for an existing organization, so none has a reason to mint a new one.

**JSON input:**

| Field | Type | Validation |
| --- | --- | --- |
| `name` | `string` | Required, 1–200 characters |
| `kind` | `string` (enum) | Required; must be `Client` or `Vendor` |

**JSON output — `201 Created`** (`Location: /api/organizations/{id}`): an `OrganizationListItem`.

**Errors:**
- `400` validation problem — blank/oversized name, unrecognised `kind`.
- `403` with `code: "organization.action_not_permitted_for_role"` — caller isn't an Admin.
- `409` with `code: "organization.duplicate_name"` — another organization already has this name.

**Business logic:** the endpoint pre-checks the name for a friendly conflict before calling
`Organization.Create`, then falls back to catching the database's own unique-index violation if
two requests race past that check at the same time — the index, not the pre-check, is the actual
guarantee.

#### `PUT /api/organizations/{organizationId}`

Renames an organization. `kind` is immutable and isn't part of this call — changing client/vendor
after a request or quote already references the organization would silently invalidate what those
records depend on.

**Auth:** any authenticated user, but only succeeds for **Admin**.

**JSON input:**

| Field | Type | Validation |
| --- | --- | --- |
| `name` | `string` | Required, 1–200 characters |

**JSON output — `200 OK`:** the updated `OrganizationListItem`.

**Errors:**
- `400` validation problem — blank/oversized name.
- `403` with `code: "organization.action_not_permitted_for_role"` — caller isn't an Admin.
- `404` — organization not found.
- `409` with `code: "organization.duplicate_name"` — another organization already has this name.

#### `POST /api/organizations/{organizationId}/retire`

Soft-deletes an organization: it stops being offered for new associations, but existing requests
and quotes that already reference it are untouched.

**Auth:** any authenticated user, but only succeeds for **Admin**.

**JSON input:** none.

**JSON output — `200 OK`:** the updated `OrganizationListItem`, with `retiredAt` now set.

**Errors:**
- `403` with `code: "organization.action_not_permitted_for_role"` — caller isn't an Admin.
- `404` — organization not found.

**Business logic:** retiring an already-retired organization is a no-op (idempotent, same shape as
`Request.Cancel`'s already-cancelled case) rather than an error.

### Health and diagnostics

- `GET /health` — anonymous, plain liveness check (no auth wiring, no database call).
- `GET /openapi/v1.json` — anonymous, the generated OpenAPI document.
- `GET /scalar/v1` — anonymous, **development environment only** — an interactive API reference UI
  (Scalar) for exploring every endpoint above by hand.
