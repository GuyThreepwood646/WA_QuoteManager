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


| Tool     | Version           | Notes                                                                                                                |
| -------- | ----------------- | -------------------------------------------------------------------------------------------------------------------- |
| .NET SDK | 10.0.100+         | Pinned via `global.json` (`rollForward: latestMinor`), so any 10.x SDK works.                                        |
| Node.js  | 22.x              | `react-router` 7.18.1 is used specifically because it has no Node-version floor above 20; any current Node 22 works. |
| npm      | bundled with Node | No separate install needed.                                                                                          |


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
3. Seeds demo data on an empty database (idempotent — running again is a no-op once data exists),
  including five organizations with profile data (addresses, contacts, location lists, and
  preferred-vendor flags).
4. Serves the API and the SPA from the same origin: **[http://localhost:5080](http://localhost:5080)**.



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

Open **[http://localhost:5173](http://localhost:5173)**. The browser only ever talks to one origin; Vite's dev proxy
forwards API calls to the backend, so there's no CORS configuration anywhere in the app.

### Demo credentials

The seeded database includes one account per role, all sharing the password below:


| Email                              | Role      | Organization                      |
| ---------------------------------- | --------- | --------------------------------- |
| `admin@warehouseanywhere.test`     | Admin     | — (platform staff)                |
| `requester@warehouseanywhere.test` | Requester | Meridian Pharma Sampling (client) |
| `reviewer@warehouseanywhere.test`  | Reviewer  | Palmetto Retail & CPG (client)    |
| `vendor@warehouseanywhere.test`    | Vendor    | SecureBase Self Storage           |
| `vendor2@warehouseanywhere.test`   | Vendor    | Crateworks Packing & Crating      |
| `vendor3@warehouseanywhere.test`   | Vendor    | Interstate Freight Partners       |


**Password (all accounts):** `Demo!2345`

This password is deliberately not secret — it's published here on purpose, and the seed hashes it
the same way a real signup would. Each account also has a seeded address and phone number, and can
be edited (including changing this password) from the Users screen — see [Users](#users) below.

### Configuration

Defaults live in `src/QuoteManager.Api/appsettings.json` and need no changes to run locally:


| Setting                          | Default                                | Purpose                                                                                                                                                                          |
| -------------------------------- | -------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ConnectionStrings:QuoteManager` | `Data Source=quotemanager.db`          | SQLite file path (gitignored, created on first run).                                                                                                                             |
| `Jwt:SigningKey`                 | a committed demo key                   | HS256 signing key for bearer tokens. **Not fit for production** — swap it via environment variable / user secrets before deploying anywhere real.                                |
| `Jwt:Issuer` / `Jwt:Audience`    | `QuoteManager` / `QuoteManager.Client` | Token validation parameters.                                                                                                                                                     |
| `AzureMonitor:ConnectionString`  | unset                                  | If present, OpenTelemetry exports to Azure Monitor. Absent by default — telemetry still works locally via the console/OTel pipeline.                                             |
| `ServiceBus:ConnectionString`    | unset                                  | If present, integration events publish to Azure Service Bus. Absent by default — an in-process channel adapter is used instead, so outbox/messaging works with zero cloud setup. |
| `KeyVault:Uri`                   | unset                                  | If present, secrets are additionally loaded from Azure Key Vault (see below). Absent by default — everything above reads from `appsettings.json` / user secrets / env vars as usual. |

#### Secrets: Azure Key Vault (optional)

`Jwt:SigningKey` is the one setting above that's a real secret rather than a connection detail, and
the committed value is explicitly a demo placeholder. `Program.cs` wires up the same "adapter only
activates when configured" pattern already used for Azure Monitor and Service Bus above: if
`KeyVault:Uri` is set, `builder.Configuration.AddAzureKeyVault(...)` layers the vault's secrets on
top of the existing configuration, and a same-named secret there overrides the local value; if it's
unset (the default), the call is skipped entirely and configuration works exactly as it does today.

```json
{
  "KeyVault": { "Uri": "https://your-vault-name.vault.azure.net/" }
}
```

- Authentication uses `DefaultAzureCredential` — a managed identity when running in Azure, or the
  logged-in `az`/Visual Studio/VS Code credential for local development against a real vault. No
  vault secret or connection string is ever hardcoded to reach it.
- Key Vault secret names can't contain `:`, so nested keys use `--` instead — the signing key above
  would be stored as a secret literally named `Jwt--SigningKey`.
- **This repository has no Azure subscription to point at**, so only the *absent* path — the actual
  default, exercised by every test in the suite — is verified end to end. The *present* path is
  scaffolding: it follows the standard, documented `Azure.Extensions.AspNetCore.Configuration.Secrets`
  integration and is straightforward to verify against a real vault, but that verification hasn't
  been (and can't be) done here.



### Running the tests

```bash
dotnet build -warnaserror   # matches CI: zero warnings tolerated
dotnet test                 # 127 tests: Domain, Architecture (dependency-direction rules), Infrastructure, API integration
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


| Role          | What this user does                                                                                                                                                                                                                                            | Organization scoping                                                                                                                                                                                                                                                                      |
| ------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Admin**     | Platform staff. Can do everything any other role can do, across every organization — create requests on behalf of any client, draft or transition a quote for any vendor, force-expire a stale quote. The one role with no organization-ownership restriction. | None (acts as any org)                                                                                                                                                                                                                                                                    |
| **Requester** | Represents a *client* company that needs storage/packing/transport. Raises new requests (`POST /api/requests`) describing what's needed and by when.                                                                                                           | Tied to one client organization, but read access is not filtered (see below)                                                                                                                                                                                                              |
| **Reviewer**  | Represents the client side evaluating offers. Moves a submitted quote into review, and decides its outcome — `StartReview`, `Accept`, `Reject`, `ReturnToSubmitted`. Cannot draft or withdraw a quote (that's the vendor's action) and cannot create requests. | Not organization-scoped — a Reviewer can review any request's quotes                                                                                                                                                                                                                      |
| **Vendor**    | Represents a storage facility, packing crew, or freight carrier responding to a request. Drafts a quote (`POST .../quotes`), then `Submit`s or `Withdraw`s it.                                                                                                 | **Strictly scoped to its own organization** — a Vendor account cannot draft, submit, or withdraw a quote belonging to a different vendor organization, and (unless also Admin/Reviewer/Requester) cannot even *read* another vendor's quote details, notes, or amount on a shared request |




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
- **Authorization lives in domain code, not endpoint attributes**: rather than `[Authorize(Roles = "...")]` on each route (which would let the API and the UI's displayed buttons silently drift
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


| Field      | Type     | Validation                                                                            |
| ---------- | -------- | ------------------------------------------------------------------------------------- |
| `email`    | `string` | Required, must be a syntactically valid email address, no leading/trailing whitespace |
| `password` | `string` | Required (non-empty)                                                                  |


**JSON output —** `200 OK`**:**

```jsonc
{
  "accessToken": "eyJ...",
  "expiresAt": "2026-07-28T20:00:00Z",
  "user": { "id": "guid", "displayName": "Ada Admin", "roles": ["Admin"], "organizationId": null }
}
```

**Errors:** `400` (validation — malformed email/blank password); `401` with `code: "auth.invalid_credentials"` for an unknown email or wrong password (the same generic message
either way, so a caller can't enumerate valid emails).

**Business logic:** looks up the user by email, verifies the submitted password against the
stored hash via `PasswordHasher.VerifyHashedPassword`. On success, issues a JWT carrying the
user's id, display name, email, one claim per role, and their organization id (if any), valid for
8 hours. No session state is kept server-side — the token *is* the session.

#### `GET /api/auth/me`

**Auth:** any authenticated user.

**JSON input:** none.

**JSON output —** `200 OK`**:** same `user` shape as login's response:

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

**JSON output —** `200 OK`**:**

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

**JSON output —** `200 OK`**:** paged envelope of `RequestListItem`:

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


| Field                  | Type        | Validation                                                |
| ---------------------- | ----------- | --------------------------------------------------------- |
| `title`                | `string`    | Required, 1–200 characters                                |
| `description`          | `string?`   | Optional, max 2000 characters                             |
| `clientOrganizationId` | `guid`      | Required (rejected as a validation error if `Guid.Empty`) |
| `neededBy`             | `datetime?` | Optional                                                  |


**JSON output —** `201 Created` (`Location: /api/requests/{id}`): a full `RequestDetailResponse`
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

**JSON output —** `200 OK`**:**

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



#### `GET /api/requests/{requestId}/activity`

**Auth:** any authenticated user.

**JSON input:** `requestId` (route, `guid`); `page`, `pageSize` (query).

**JSON output —** `200 OK`**:** paged envelope of `ActivityEntryResponse`:

```jsonc
{
  "id": "guid",
  "subjectType": "Request|Quote",
  "subjectId": "guid",
  "action": "QuoteAccepted",
  "summary": "string",
  "actorDisplayName": "string",
  "occurredAt": "datetime",
  "note": "string?"
}
```

Newest first, ties broken by id (which sorts chronologically since it's a UUIDv7).

**Errors:** `404` if `requestId` doesn't exist.

**Business logic:** per-request "what happened" timeline, read directly from the `AuditEntry` table — a transactional projection of domain events written in the *same* database
transaction as the change that raised them (never from application logs, which are a diagnostics
concern, not the audit source of truth). Applies the identical visibility rule as the request
detail endpoint: a pure Vendor viewer only sees `Quote`-subject rows for their own organization's
quote; every `Request`-subject row (created, vendor invited, awarded, cancelled) names no vendor
and carries no money, so it's visible to anyone who can see the request at all.

`note` is the free-text explanation an actor optionally typed when they made a quote transition
(see `POST .../transitions` below) — `null` for every other kind of entry, and for a transition
where nobody supplied one. It is carried as its own field rather than folded into `summary`, so the
timeline can render the machine-generated sentence and the human's own words as two distinct lines.

### Quotes



#### `POST /api/requests/{requestId}/quotes`

**Auth:** any authenticated user, but only succeeds when the caller may act for the named vendor
organization (see business logic).

**JSON input:** `requestId` (route, `guid`); body:


| Field                  | Type        | Validation                                     |
| ---------------------- | ----------- | ---------------------------------------------- |
| `vendorOrganizationId` | `guid`      | Required, must not be `Guid.Empty`             |
| `amount`               | `decimal`   | Required, must be greater than zero            |
| `currency`             | `string`    | Required, exactly 3 characters (ISO-4217 code) |
| `expiresAt`            | `datetime?` | Optional                                       |
| `notes`                | `string?`   | Optional, max 2000 characters                  |


**JSON output —** `201 Created` (`Location: .../quotes/{id}`, `ETag` header carrying the quote's
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
with `code: "request.not_editable"` if the request isn't `Open`; `403` with `code: "quote.action_not_permitted_for_role"` if the caller can't act for `vendorOrganizationId`.

**Business logic:** `Request.AddQuote` is the sole authority here. It checks the request is still
`Open`, then checks `actor.CanActForVendorOrganization(vendorOrganizationId)` — true for Admin
unconditionally, true for a Vendor only when their own organization id matches the id in the
request body. This is what stops a vendor account from drafting a quote *under a competitor's
name* and leaving it for that competitor to discover. The new quote starts in `Draft`.

#### `GET /api/requests/{requestId}/quotes/{quoteId}`

**Auth:** any authenticated user.

**JSON input:** `requestId`, `quoteId` (route, `guid`).

**JSON output —** `200 OK` (with a weak `ETag` response header equal to the quote's `version`):
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
`GET` endpoint or the previous transition's response. Missing or malformed → `400` with `code: "quote.if_match_required"`, checked before any domain logic runs.

**JSON input — body:**


| Field    | Type            | Validation                                                                                                                                                                               |
| -------- | --------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `action` | `string` (enum) | Required; must be one of `Submit`, `StartReview`, `ReturnToSubmitted`, `Accept`, `Reject`, `Withdraw`, `Expire` (`Edit` is a recognised enum value but is rejected — see business logic) |
| `note`   | `string?`       | Optional, max 2000 characters — a free-text explanation for this specific transition                                                                                                   |


**JSON output —** `200 OK` (fresh `ETag` header): the updated `QuoteResponse`.

**Errors:**

- `400` validation problem — `action` isn't a recognised value, or `note` exceeds 2000 characters.
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


| From        | Action              | To          | Who                      |
| ----------- | ------------------- | ----------- | ------------------------ |
| Draft       | `Submit`            | Submitted   | Vendor (own org) / Admin |
| Draft       | `Withdraw`          | Withdrawn   | Vendor (own org) / Admin |
| Submitted   | `StartReview`       | UnderReview | Reviewer / Admin         |
| Submitted   | `Withdraw`          | Withdrawn   | Vendor (own org) / Admin |
| Submitted   | `Expire`            | Expired     | Admin only               |
| UnderReview | `Accept`            | Accepted    | Reviewer / Admin         |
| UnderReview | `Reject`            | Rejected    | Reviewer / Admin         |
| UnderReview | `ReturnToSubmitted` | Submitted   | Reviewer / Admin         |
| UnderReview | `Expire`            | Expired     | Admin only               |


Terminal states (no action legal from any of these, for anyone): `Accepted`, `Rejected`,
`Withdrawn`, `Expired`.

`Edit` (business-field changes to an already-drafted quote) travels in `permittedActions` as a
signal for the UI, but is **not** a status transition — sending it to this endpoint is rejected
before the transition table is even consulted, since it's a different kind of change (there is no
dedicated edit-quote endpoint yet).

**`note` explains *this* transition, not the quote's current state.** It is never stored on the
quote itself (unlike `statusReason`, which the system alone sets for the one automatic case below)
— it only ever reaches the request's activity timeline, as its own field on that transition's
`ActivityEntryResponse` entry (see `GET .../activity` above). A reviewer rejecting a quote and
typing "Price came in over budget" leaves that note attached to the `QuoteRejected` timeline entry;
it does not change anything a later `GET` of the quote itself returns.

**Accepting a quote has side effects beyond itself**, all inside the same database transaction:
every sibling quote on the same request still in `Submitted` or `UnderReview` is automatically
rejected (`statusReason: "SupersededByAcceptedQuote"`), and the parent request's status flips to
`Awarded`. This is what guarantees at most one accepted quote per request — enforced twice, once
here in the aggregate and again as a database-level filtered unique index, so even a race between
two concurrent accept attempts can't leave two accepted quotes.

Every transition, not just `Accept`, is concurrency-checked via the `If-Match`/`version` pair —
two reviewers racing to decide the same quote will have the second one rejected with `409 quote.concurrent_modification` rather than silently overwriting the first.

### Organizations

The organization directory holds client companies and vendor partners. Each record carries a
**profile** (primary address, primary contact, optional additional location addresses) and, for
vendors, an **isPreferredVendor** flag. The UI lists name and contact summary in the table row,
expands a row for full details, and lets an Admin edit the profile in place.

`OrganizationListItem` (returned by every organizations endpoint below):

```jsonc
{
  "id": "guid",
  "name": "string",
  "kind": "Client|Vendor",
  "retiredAt": "datetime|null",
  "primaryAddress": "string|null",
  "primaryContactName": "string|null",
  "primaryContactEmail": "string|null",
  "primaryContactPhone": "string|null",
  "isPreferredVendor": false,
  "locations": [
    { "id": "guid", "address": "string", "phone": "string|null", "sortOrder": 0 }
  ]
}
```

`kind` is immutable after create. `isPreferredVendor` is stored only for **Vendor** organizations
(the domain ignores it for clients). Blank optional strings are stored as `null`. Locations are
replaced wholesale on update — the request body is the full desired list. Each location requires an
`address`; `phone` is optional.

#### `GET /api/organizations`

**Auth:** any authenticated user.

**JSON input:** query string only — `page`, `pageSize`, `includeRetired` (default `false`; when
`true`, retired organizations are included — the Organizations screen passes this for Admin).

**JSON output —** `200 OK`**:** paged envelope of `OrganizationListItem` (see above).

**Business logic:** a simple, unfiltered directory — every authenticated user reads every
organization (there's no per-user visibility rule here; the rule that matters, vendor-quote
visibility, lives on the requests/quotes endpoints above). Used by the UI to render the
Organizations screen (table summary + expandable detail panel) and to resolve names for display
elsewhere. Retired organizations are excluded from pickers by default (`includeRetired=false`).

#### `POST /api/organizations`

**Auth:** any authenticated user, but only succeeds for **Admin** (enforced in the domain).

**JSON input:**


| Field                 | Type       | Validation                                                      |
| --------------------- | ---------- | --------------------------------------------------------------- |
| `name`                | `string`   | Required, 1–200 characters                                      |
| `kind`                | `string`   | Required — `Client` or `Vendor`                                 |
| `primaryAddress`      | `string?`  | Optional, max 500 characters                                    |
| `primaryContactName`  | `string?`  | Optional, max 200 characters                                    |
| `primaryContactEmail` | `string?`  | Optional, max 320 characters, must be a valid email if provided |
| `primaryContactPhone` | `string?`  | Optional, max 50 characters                                     |
| `isPreferredVendor`   | `bool`     | Optional, default `false` — rejected for `kind: "Client"`       |
| `locations`           | `object[]` | Optional — additional sites; see `OrganizationLocationRequest` below |


`OrganizationLocationRequest` (each entry in `locations`):

| Field     | Type      | Validation                          |
| --------- | --------- | ----------------------------------- |
| `address` | `string`  | Required, 1–500 characters          |
| `phone`   | `string?` | Optional, max 50 characters         |


**JSON output —** `201 Created` (`Location: /api/organizations/{id}`): an `OrganizationListItem`
for the newly created organization.

**Errors:**

- `400` validation problem — blank/oversized name, invalid `kind`, invalid email, or
`isPreferredVendor: true` on a client organization.
- `403` with `code: "organization.action_not_permitted_for_role"` — caller is not Admin.
- `409` with `code: "organization.duplicate_name"` — another organization already has this name.

**Business logic:** creates the aggregate via `Organization.Create`, then applies the optional
profile fields via `Organization.UpdateProfile` in the same transaction. Names are globally unique
(including retired organizations).

#### `PUT /api/organizations/{organizationId}`

**Auth:** any authenticated user, but only succeeds for **Admin** (enforced in the domain).

**JSON input:** `organizationId` (route, `guid`) plus the same profile fields as `POST` above
(`name` required; `kind` is **not** accepted — it cannot change).

**JSON output —** `200 OK`**:** the updated `OrganizationListItem`.

**Errors:**

- `400` validation problem — blank/oversized name, invalid email, or `isPreferredVendor: true` on a
client organization.
- `403` with `code: "organization.action_not_permitted_for_role"` — caller is not Admin.
- `404` — unknown `organizationId`.
- `409` with `code: "organization.duplicate_name"` — rename would collide with another organization.

**Business logic:** updates name and the full profile (contact fields, preferred-vendor flag,
location list) via `Organization.UpdateProfile`. This is the endpoint the Organizations detail
panel calls on **Save**.

#### `POST /api/organizations/{organizationId}/retire`

**Auth:** any authenticated user, but only succeeds for **Admin** (enforced in the domain).

**JSON input:** `organizationId` (route, `guid`) only.

**JSON output —** `200 OK`**:** the retired `OrganizationListItem` (`retiredAt` set).

**Errors:**

- `403` with `code: "organization.action_not_permitted_for_role"` — caller is not Admin.
- `404` — unknown `organizationId`.

**Business logic:** soft-deletes the organization (`RetiredAt` set). Existing requests, quotes,
invitations, and user links are untouched; the organization simply stops appearing in pickers
(`includeRetired=false`). Retiring again is a no-op.

### Users

User account management: creating accounts, editing profile fields, and changing passwords.
Unlike `Organization`/`Request`, `AppUser` has no domain aggregate of its own — by design, so a
password hash is never at risk of being treated as part of a business aggregate — so the
permission checks below are enforced directly in the endpoint, using the same typed exceptions
and RFC 9457 problem-details mapping every other feature uses.

**Visibility is scoped, not blocked**: `GET /api/users` returns every user to an **Admin**, but
anyone else's result is filtered to only their own row — the same "filter, don't refuse" rule
`GET /api/requests` already applies to quote visibility for a Vendor. This is what lets one
endpoint and one screen serve both the Admin's user directory and everyone else's "my profile"
view. **Only an Admin can create a user or change anyone's roles/organization — including their
own** (a non-Admin can only edit their own display name, email, address, and phone).

`UserListItem` (returned by every users endpoint below):

```jsonc
{
  "id": "guid",
  "email": "string",
  "displayName": "string",
  "roles": ["Requester"],
  "organizationId": "guid|null",
  "organizationName": "string|null",
  "address": "string|null",
  "phone": "string|null"
}
```

**Password requirements**, enforced identically wherever a password is set (creating a user or
resetting one) — the UI shows this exact list, live, as red-X/green-check while typing:

| Requirement | Rule |
| --- | --- |
| Length | At least 8 characters |
| Uppercase | At least one uppercase letter |
| Lowercase | At least one lowercase letter |
| Number | At least one digit |
| Special character | At least one non-alphanumeric character |

#### `GET /api/users`

**Auth:** any authenticated user.

**JSON input:** query string — `page`, `pageSize`.

**JSON output —** `200 OK`**:** paged envelope of `UserListItem`.

**Business logic:** an **Admin** gets every user, ordered by display name. Anyone else gets a
single-item result containing only their own row — there is no way for a non-Admin to enumerate
or read any other user's email, address, or phone through this endpoint.

#### `POST /api/users`

**Auth:** any authenticated user, but only succeeds for **Admin**.

**JSON input:**

| Field | Type | Validation |
| --- | --- | --- |
| `email` | `string` | Required, valid email, unique |
| `displayName` | `string` | Required, 1–200 characters |
| `roles` | `string[]` | Required, non-empty, each one of `Requester`/`Reviewer`/`Vendor`/`Admin` |
| `organizationId` | `guid?` | Required unless `roles` is exactly `["Admin"]`; must reference an existing organization |
| `address` | `string?` | Optional, max 500 characters |
| `phone` | `string?` | Optional, max 50 characters |
| `password` | `string` | Required, must satisfy every rule above |
| `confirmPassword` | `string` | Required, must equal `password` |

**JSON output —** `201 Created`**:** the new `UserListItem`.

**Errors:**

- `400` validation problem — any field above fails its rule (each unmet password requirement is
its own error against `password`).
- `400` with `code: "user.unknown_organization"` — `organizationId` doesn't reference an existing
organization.
- `403` with `code: "user.action_not_permitted_for_role"` — caller isn't Admin.
- `409` with `code: "user.duplicate_email"` — another user already has this email.

**Business logic:** the password is hashed with the same `IPasswordHasher<AppUser>` the demo seed
and login already use — never stored or logged in plain text.

#### `PUT /api/users/{userId}`

**Auth:** any authenticated user; succeeds for the user themselves or an Admin — see below for
what a non-Admin self-edit can and can't change.

**JSON input:**

| Field | Type | Validation |
| --- | --- | --- |
| `email` | `string` | Required, valid email, unique |
| `displayName` | `string` | Required, 1–200 characters |
| `address` | `string?` | Optional, max 500 characters |
| `phone` | `string?` | Optional, max 50 characters |
| `roles` | `string[]` | Same rule as create — **ignored unless the caller is Admin** |
| `organizationId` | `guid?` | Same rule as create — **ignored unless the caller is Admin** |

**JSON output —** `200 OK`**:**

```jsonc
{
  "user": { /* UserListItem */ },
  "accessToken": "string|null",
  "expiresAt": "datetime|null"
}
```

**Errors:**

- `400` validation problem, or `user.unknown_organization` — same as create.
- `403` with `code: "user.action_not_permitted_for_role"` — a non-Admin tried to change their own
`roles` or `organizationId` to a different value than they already had.
- `404` — either the user doesn't exist, **or** the caller is a non-Admin editing someone other
than themselves. Both cases return the identical 404, the same reason
`GET /api/requests/{id}/quotes/{quoteId}` refuses a competitor's quote with 404 rather than 403 —
a caller with no visibility right to another user's account shouldn't be able to confirm which
ids are real ones from the status code alone.
- `409` with `code: "user.duplicate_email"` — another user already has this email.

**Business logic — session staleness:** `displayName`, `email`, and `roles` are baked into the JWT
at login (`TokenService.IssueFor`) and never re-read from the database per request. Editing
**your own** account therefore reissues a fresh token in the response (`accessToken`/`expiresAt`
are populated only in that case) — the SPA calls the same session-storage function login already
uses, so your own header never shows a stale name after you save. **Editing someone else's**
account has no such fix: that person's already-open session keeps showing their old name/email/
roles until they next log in. This is accepted, documented behavior rather than something this
endpoint solves — building a token-revocation or refresh system for it was judged disproportionate
to a demo app.

#### `POST /api/users/{userId}/reset-password`

**Auth:** any authenticated user; succeeds for the user themselves or an Admin.

**JSON input:**

| Field | Type | Validation |
| --- | --- | --- |
| `currentPassword` | `string?` | Required (and must be correct) when resetting **your own** password; ignored when an Admin resets someone else's |
| `newPassword` | `string` | Required, must satisfy every password rule above |
| `confirmNewPassword` | `string` | Required, must equal `newPassword` |

**JSON output —** `204 No Content`.

**Errors:**

- `400` validation problem — `newPassword` fails a requirement, or doesn't match
`confirmNewPassword`.
- `403` with `code: "user.invalid_current_password"` — resetting your own password, and
`currentPassword` didn't match. **Not `401`**, deliberately: the caller's bearer token is entirely
valid here, only the submitted current-password value is wrong, and the SPA's single network
egress (`apiClient.ts`) treats *any* `401` while a session exists as an expired token — it clears
the session and redirects to `/login`. A `401` here would silently log the user out instead of
showing them an inline "wrong password" message.
- `404` — same identical-404 rule as `PUT` above: either the user doesn't exist, or a non-Admin is
targeting someone other than themselves.

**Business logic:** an Admin resetting someone else's password never needs (or is asked for) that
person's current password — admin authority substitutes for that proof, the same reason an admin
password reset exists at all (a user who forgot their password couldn't supply it either way).
Resetting your own password always requires proving you know the current one. Either way, the
result is hashed with `IPasswordHasher<AppUser>`; no token reissue is needed here, since a password
is never itself a JWT claim.

### Health and diagnostics

- `GET /health` — anonymous, plain liveness check (no auth wiring, no database call).
- `GET /openapi/v1.json` — anonymous, the generated OpenAPI document.
- `GET /scalar/v1` — anonymous, **development environment only** — an interactive API reference UI
(Scalar) for exploring every endpoint above by hand.

### Content Security Policy

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

