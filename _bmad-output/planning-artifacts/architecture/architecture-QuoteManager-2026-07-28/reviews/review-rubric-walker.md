---
title: 'Rubric Walker Review — ARCHITECTURE-SPINE.md (QuoteManager)'
reviewer: rubric-walker
gate: bmad-architecture Reviewer Gate
target: ARCHITECTURE-SPINE.md
target_status_at_review: draft
reviewed: 2026-07-28
sources_read:
  - _bmad-output/planning-artifacts/architecture/architecture-QuoteManager-2026-07-28/ARCHITECTURE-SPINE.md
  - docs/inputs/challenge-brief.md
  - _bmad-output/planning-artifacts/architecture/architecture-QuoteManager-2026-07-28/.memlog.md
---

# Rubric Walker Review — QuoteManager Architecture Spine

## Verdict

**CHANGES REQUIRED — not a rewrite.**

The spine is above average where it matters most: AD-2 (single transition table) and AD-7 (server-projected `permittedActions`) correctly identify and close the single highest-probability divergence in a two-day full-stack build, and AD-5's separation of transactional audit from diagnostic logging is the right reading of an ambiguous FR-5. The paradigm is coherent and the layering is enforceable.

It is not yet a safe build substrate for the epics/stories layer, for three blocking reasons and one delivery-risk reason:

1. **The authorization dimension is silent while AD-7 depends on it.** AD-7 promises an actor-aware permitted-action set; AD-2 declares itself the sole authority and has no actor axis. No role set exists anywhere in the spine, though RBAC is an explicit role-derived requirement in the brief. The two halves of the build will diverge at exactly the seam AD-7 was written to protect.
2. **AD-6 contradicts the Deferred table and the deployment diagram**, and its verification clause is unexecutable given no active Azure subscription.
3. **How the frontend obtains, stores, and attaches the JWT is an entirely undecided dimension** — and the conventions table's "one generated API client module per resource" multiplies that divergence across every frontend story.
4. **Scope is not deliverable in two days including rehearsal** as currently mandated, and no AD or section names a demo-critical minimum versus stretch ordering. Under the stated pass condition ("correct but unbuildable in two days is a FAILURE"), that is a first-class finding, not a nicety.

Brief coverage (FR-1 to FR-5) is nominally complete and FR-4 is handled with genuine intent rather than a CRUD grid — but FR-1 and FR-4 are both under-specified in ways that will diverge across stories (see H6, M7).

---

## Checklist Walk

### 1. Does it fix the real divergence points for the level below?

**Partially.** Fixed well and unambiguously: dependency direction (AD-1), lifecycle authority (AD-2), accepted-quote uniqueness including the concurrent case (AD-3), publish path (AD-4), audit transactionality and audit-vs-logging (AD-5), client-encodes-no-rules (AD-7), error envelope (AD-8), actor provenance (AD-10), read-model shape (AD-11). The conventions table also correctly pre-decides several classic per-story coin flips: money type, id generation, time source, collection envelope, log style.

Missed divergence points, each of which two independently-built stories will resolve differently:

- The role/authorization model and its relationship to the transition table (C1).
- Frontend token acquisition, storage, attachment, and 401 handling (C3).
- Domain-event to integration-event mapping — which events actually leave the process (H3).
- Migration application, seeding, and demo credentials (H4).
- The `Request` status set and the load-bearing entity attributes (H5).
- Field mutability by status — AD-2 governs *status* transitions only, nothing governs whether a `Submitted` quote's price is editable (H6).
- List query parameters: paging, sorting, filtering (H7).
- Enum persistence form, which AD-3's own rule text silently assumes (M2).
- Error-code catalogue and the client-side code-to-copy mapping (M8).
- The OpenAPI-to-TypeScript client generator (M6).

### 2. Is every AD's Rule enforceable, and does it prevent what Prevents claims?

| AD | Enforceable as written | Prevents claim holds | Note |
| --- | --- | --- | --- |
| AD-1 | Yes — architecture test named | Yes | Test library unnamed and absent from Stack (M6); "fails the build" needs the CI/test-run binding (M11) |
| AD-2 | Yes for status; **incomplete** | Partially | No actor axis, so it cannot be the sole authority AD-7 needs (C1); says nothing about field mutability (H6) |
| AD-3 | Yes — aggregate check + DB index + 409 mapping | Yes | Best-constructed AD in the spine; index filter literal assumes string enums (M2) |
| AD-4 | Yes mechanically, but **over-broad** | **No** | As written it publishes every domain event, contradicting the memlog; the Prevents text credits the outbox with audit trustworthiness that AD-5 says audit does not depend on (H3) |
| AD-5 | Yes | Yes | Strong; the "diagnostics are not audit" line is the correct call |
| AD-6 | **No** | **No** | "Both adapters verified by the same contract test suite" cannot execute without Azure; "every outbound dependency" is false for persistence (C2) |
| AD-7 | Server half yes; client half **review-only** | Only if C1 is fixed | "No TypeScript may branch on status" is aspirational prose without a lint rule (L5) |
| AD-8 | Server half yes; client half **not enforceable** | Partially | The "closed set of stable identifiers" is asserted but never enumerated or given an owner, so nothing can be checked against it (M8) |
| AD-9 | Yes — and it will fire on things you need open | Yes | The deny-by-default fallback will 401 the SPA fallback route and the Scalar UI (H1) |
| AD-10 | Yes — unknown-member drop is default `System.Text.Json` behaviour | Yes | Make the mechanism explicit so nobody adds `[JsonExtensionData]` |
| AD-11 | **Partially** | Yes | "computed in the projection **or** from stored columns" hands the story author a choice a spine exists to remove (M7) |

Aspirational-prose rules to tighten: AD-6's contract-test clause, AD-7's "no TypeScript may branch on status", AD-8's "the UI branches on `code` only" and its unenumerated closed set, AD-8's "controllers contain no try/catch".

### 3. Could anything Deferred let two independently-built units diverge?

Legitimate deferrals (cannot cause divergence): refresh tokens/revocation, horizontal outbox scale-out, real notification transport, Azure provisioning, performance budgets, TypeScript 7.

Divergence-capable deferrals:

- **"Multi-tenancy and row-level authorisation."** Once C1 introduces a vendor-shaped role, "does a vendor list endpoint return other vendors' quotes?" becomes a per-story coin flip. The deferral is only safe if the spine states affirmatively that in this build every authenticated user sees all data. (Folded into C1.)
- **"A second EF provider and SQL Server migration set."** The deferral itself is sound, but it directly contradicts AD-6, which instructs a story author to build a config-gated cloud adapter for persistence (C2).
- **"Playwright end-to-end suite."** Not a divergence risk, but a brief-coverage risk: the role explicitly requires unit, integration, **and** end-to-end tests (M12).

### 4. Are named technologies verified-current and internally consistent?

The memlog documents same-day verification against the NuGet flat-container API and the npm registry, and the pinning decisions (TypeScript 6.0.3 over 7.0.2 for the typescript-eslint programmatic-API gap; Shouldly over licensed FluentAssertions; in-box OpenAPI plus Scalar over Swashbuckle; Azure Monitor OTel Distro over the maintenance-mode Application Insights SDK) are each individually well-reasoned and correctly justified. The `net10.0` / ASP.NET Core 10.0.10 / EF Core 10.0.10 / JwtBearer 10.0.10 / Mvc.Testing 10.0.10 band is internally consistent. React 19.2.8 with Mantine 9.5.0, TanStack Query 5.101.4, React Router 8.3.0, Vite 8.1.5 on Node 22.18.0 is a coherent set.

Consistency problems are about **omissions**, and one is a genuine resolution hazard:

- The deployment diagram requires a **local OpenTelemetry console exporter**, but no OpenTelemetry core packages (`OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Exporter.Console`) appear in the Stack table. `Azure.Monitor.OpenTelemetry.AspNetCore` 1.6.0 pins the OTel core transitively; unpinned direct references are the one realistic "cannot resolve together" diamond in this stack. Pin them explicitly to the versions the distro carries.
- No architecture-test library (AD-1's enforcement mechanism), no ESLint/typescript-eslint (verified in the memlog, dropped from the Stack), and no OpenAPI-to-TypeScript generator (required by the "one generated API client module per resource" convention).
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore` is listed, but the memlog decides `AddIdentityCore` plus `PasswordHasher` only, and the ER diagram shows a custom `APP_USER` rather than `AspNetUsers`. Three sources, three implied storage models (M5).

### 5. Brownfield ratification — skipped (greenfield).

### 6. Does the spine cover every capability in the brief?

| FR | Covered | Assessment |
| --- | --- | --- |
| FR-1 Entity management | Mapped to `Application/Features/*` | **Thin.** "Manage" is unpacked nowhere: no create/edit/delete-or-archive semantics, no per-status field mutability (H6), no entity attributes in the structural seed (H5) |
| FR-2 Many quotes, differing stages | AD-2, AD-3, `Request` aggregate | Solid |
| FR-3 UI-supported, API-enforced transitions | AD-2, AD-3, AD-7, AD-8 | Strong on the enforcement seam; **unprotected under concurrency for every action except Accept** (H2) |
| FR-4 Work visibility / triage | AD-5 timeline, AD-11 projections, attention signals | **Correctly interpreted** — reads as triage, not a grid, and the deterministic `TimeProvider` clause is a good call. But the dashboard view set is undefined, thresholds are unstated, and AD-11's "projection or stored columns" choice remains open (M7) |
| FR-5 Lightweight auditability | AD-5, AD-10, `AuditEntry` | Strongest area of the spine. Polymorphic subject addressing is the right economy for the timebox |
| Candidate intent: Service Bus | AD-4, AD-6, Stack | Present in code, gated — matches the constraint |
| Role-derived: RBAC | AD-9 "role gates declared per endpoint" | **Not actually covered.** No role set exists (C1) |
| Role-derived: E2E tests | Deferred | Gap against a stated requirement (M12) |

FR-4 specifically is not missed — it is the second-best-served requirement in the spine. It is, however, the requirement most likely to be built inconsistently, because AD-11 names the signals without deciding their computation site or thresholds.

### 7. Inheritance conflicts — skipped (no parent spine).

### 8. Is every dimension this altitude owns decided, deferred, or named as an open question?

| Dimension | State | Finding |
| --- | --- | --- |
| Deployment / hosting topology | **Decided** — good diagram, same-build-two-profiles | Persistence leg of the Azure profile is false (C2); governed only by a diagram, no AD binds it |
| Environments / config layering | Partial | Only `appsettings.Development.json` is mentioned; environment set unstated (M1) |
| Infra/provider strategy | Decided (SQLite single provider) | Contradicted by AD-6 (C2) |
| Operations (start-up, run contract) | **Silent in spine** | Migration/seed/one-command run lives only in the memlog (H4) |
| Concurrency / optimistic concurrency control | **Silent** — whole dimension | H2 |
| Pagination | Response envelope decided; **request contract silent** | H7 |
| Sorting / filtering | **Silent** | H7 |
| DB migration + seeding | **Silent in spine** (memlog only) | H4 |
| CORS / SPA hosting | Partial — static-from-API decided; fallback routing, `dist`-to-`wwwroot` build step, CORS posture, frontend API base URL all silent | H1 |
| Secret handling | Partial and **self-contradictory** | M1 |
| Health checks / readiness | **Silent** — implied only by AD-9's `AllowAnonymous` exception | M3 |
| Log-to-trace correlation | Decided at convention level; mechanism and pipeline ownership silent | L1 |
| Frontend JWT acquisition/storage | **Silent** — whole dimension | C3 |
| Authorization / role model | **Silent** — whole dimension | C1 |
| Domain-event to integration-event mapping | **Silent** | H3 |
| Enum persistence | **Silent**, and assumed by AD-3's rule text | M2 |
| Expiry mechanism | Referenced in three places, governed nowhere | M9 |
| CI gate content | Tree entry only, no rule | M11 |
| Scope ordering / demo-critical minimum | **Silent** | H8 |

Four whole dimensions are silent: authorization, frontend auth plumbing, concurrency control, and operations/start-up. Each is a finding in its own right below.

---

## Findings

### CRITICAL

#### C1 — AD-2 / AD-7 / AD-9: the authorization dimension is silent, and AD-7 structurally depends on it

**Where:** AD-2 (transition table), AD-7 (`permittedActions`), AD-9 ("role gates are declared per endpoint"), Deferred ("row-level authorisation").

**Problem.** AD-7 states that `permittedActions` is "computed server-side from the AD-2 table **for the current actor** and state." AD-2's table maps `(QuoteStatus, QuoteAction) -> QuoteStatus` — there is no actor or role dimension in it — and AD-2 declares itself "the only place a legal transition is expressed." These two rules cannot both be true. Meanwhile no role set is named anywhere in the spine, the memlog, or the capability map, despite role-based access control being an explicit role-derived requirement of the brief and `Organization` filling two distinct roles (client and vendor) in the structural seed.

The consequence at story level is precisely the divergence AD-7 exists to prevent: the endpoint story will express authorization as `[Authorize(Roles = ...)]` attributes, and the projection story will compute `permittedActions` from status alone. The UI will then offer actions the API rejects with 403 — the same failure mode as offering actions it rejects with 409, just with a different status code. The deferral of "row-level authorisation" compounds it: with a vendor-shaped role in play, "does a vendor's list endpoint show other vendors' quotes?" becomes a per-story coin flip, which makes that deferral illegitimate under checklist item 3.

**Suggested fix.** Add an AD (say AD-12) and amend AD-2:

- Name the role set. Minimum viable for the demo and the brief: `Admin`, `Requester` (raises requests on behalf of a client org), `Reviewer` (may `StartReview`, `Accept`, `Reject`), `Vendor` (may create and `Withdraw` its own org's quotes). Three roles would also be defensible; zero is not.
- Widen the transition table to `(QuoteStatus, QuoteAction) -> (QuoteStatus, RequiredRoles)` and expose exactly one Domain function, `QuoteTransitions.PermittedFor(status, actorRoles)`. **Both** the endpoint's authorization check and the `permittedActions` projection must call that one function. No `Roles = "..."` string literal may appear on a transition endpoint.
- State the row-level posture affirmatively for this build: either every authenticated user reads all data (and say so in the README and demo script), or the org-scoped filter exists now as a single EF global query filter. Do not leave it deferred and unstated.

#### C2 — AD-6: the rule is false for persistence, contradicts the Deferred table and the Azure diagram, and its verification clause cannot execute

**Where:** AD-6, Deferred row 3, "Deployment and operational envelope" diagram.

**Problem.** Three independent defects in one AD:

1. AD-6 binds "persistence" and rules that *each* port has "exactly two implementations", a local default and an Azure one gated on a connection string. The Deferred table simultaneously rules out "a second EF provider and SQL Server migration set." Both cannot hold. A story author following AD-6 will build a second persistence adapter that the Deferred table says does not exist.
2. The Azure profile diagram shows `A2 --> S2[(Relational store)]`, which the SQLite-only provider decision cannot reach. The headline claim "the two profiles are the **same build**" is therefore false as drawn, and it is false in the one place an interviewer is most likely to poke.
3. "Both adapters for a port are verified by the same contract test suite" is unexecutable: the Azure subscription is inactive by stated constraint, and no emulator is named for Service Bus or Azure Monitor. As written, this rule cannot pass, which means in practice it will be ignored — the worst outcome for a spine rule.

**Suggested fix.** Rewrite AD-6's Rule to:

- Bind only the ports that genuinely have two adapters: **messaging, telemetry, notification**. State explicitly that **persistence has exactly one provider (SQLite) in this build**, and that provider substitution is a migration-set problem, not an adapter problem — cross-referencing the Deferred row.
- Redraw the Azure profile: `S2` becomes "SQLite on App Service persistent storage" or the persistence leg is dropped from the Azure subgraph with a note. Either is honest; the current diagram is not.
- Restate verification enforceably: "the port's contract test suite runs against the local adapter in CI. Each Azure adapter is compile-verified, registered under a `[Trait("Requires","Azure")]` contract test excluded from the default run, and its registration path is covered by a configuration test asserting that an absent connection string selects the local adapter and a present one selects the Azure adapter." That last assertion is the part that actually carries risk and it is fully testable without a subscription.

#### C3 — Whole dimension silent: how the frontend obtains, stores, and attaches the JWT

**Where:** AD-9 (server side only), AD-7, Frontend conventions row.

**Problem.** AD-9 fully specifies server-side authentication and says nothing about the client. Nothing in the spine decides: the login endpoint shape, where the token lives (module memory, React context, `localStorage`, `sessionStorage`, cookie), how it is attached to requests, what happens on 401, what happens on expiry, or how route protection works with React Router 8. The conventions table then mandates "one generated API client module per resource" — so whatever is undecided here gets re-decided independently in every resource module. This is the highest-multiplicity gap in the spine.

It also collides with the deferral of refresh tokens: Deferred says access tokens have "a short lifetime", but no lifetime value is given and no client behaviour on expiry is specified. A demo with a 15-minute token and no expiry handling fails mid-rehearsal.

**Suggested fix.** Add an AD:

- `POST /api/auth/login` returns `{ accessToken, expiresAt, user: { id, displayName, roles } }`. Token lifetime is pinned at a stated value (8 hours is right for a two-day demo build — long enough that expiry never interrupts the demo, short enough to be defensible).
- The token is held in one `AuthProvider` in memory with `sessionStorage` rehydration so a page refresh does not log the reviewer out. State the XSS trade-off in one sentence so the choice reads as deliberate rather than default.
- **Exactly one** `apiClient` fetch wrapper attaches `Authorization: Bearer`, parses RFC 9457 problem details into a typed error, and is the only place in the SPA where a network request originates. Generated resource modules call it; they never call `fetch` or `axios` directly. This is lint-enforceable via `no-restricted-globals` / `no-restricted-imports`.
- On 401: clear auth state, clear the TanStack Query cache, redirect to `/login`. On 403: render a "not permitted" state without logging out.

---

### HIGH

#### H1 — AD-9's deny-by-default fallback policy will 401 the SPA shell and the API reference UI

**Where:** AD-9 ("anonymity is opt-in via explicit `AllowAnonymous` on login and health only"), the deployment note "the SPA is served as static assets from the API host".

**Problem.** A fallback authorization policy applies to every *endpoint*. `MapFallbackToFile("index.html")` — required for React Router deep links and refreshes — is an endpoint, so an unauthenticated browser hitting `/` or `/login` gets 401 and never reaches the login screen. The demo cannot start. Same for `/openapi/{doc}.json` and the Scalar UI: a reviewer browsing the API from a fresh clone gets 401 on the one surface most likely to be opened first. AD-9's enumeration is "login and health only", which explicitly excludes all three.

This is a small fix and a large failure, which is exactly the class of thing that must be pinned in the spine rather than found at 11pm on day two.

**Suggested fix.** Amend AD-9's Rule to enumerate the complete anonymous set: the login endpoint, health endpoints, the SPA fallback route and static assets, the OpenAPI document, and the Scalar reference UI — nothing else. Add an integration test that asserts unauthenticated `GET /`, `GET /login`, `GET /health/live`, and `GET /openapi/v1.json` all return 200 while a representative API route returns 401. That test is the enforcement mechanism; without it the enumeration is just prose.

#### H2 — Whole dimension silent: optimistic concurrency control for every transition except Accept

**Where:** AD-2, AD-3, FR-3.

**Problem.** AD-3 is careful and correct about the Accept race, guarding it in the aggregate *and* at the database. Nothing guards any other transition. Two reviewers acting on the same `Submitted` quote — one `StartReview`, one `Withdraw` — both read status `Submitted`, both pass the transition table, both commit; last write wins and the losing user's UI silently shows a state that was overwritten. FR-3 says transitions must be *enforced by the API*; under concurrency, they currently are not, except for Accept.

This is not a theoretical concern for a demo, because the audit trail (FR-5) will faithfully record two successful transitions from the same source state, which is visibly wrong in the one artifact designed to prove correctness.

It also needs a *decision* rather than an assumption because SQLite has no `rowversion`: EF Core cannot supply an automatic concurrency token on this provider, so a story author who reaches for `IsRowVersion()` will fail and improvise.

**Suggested fix.** Either decide it or defer it explicitly — silence is the only unacceptable option.

- Preferred: add `int Version` to `Quote` and `Request`, mark it `IsConcurrencyToken()`, increment it inside `ApplyQuoteAction`. Return it as a weak `ETag` on quote reads, require `If-Match` on the transition endpoint, and map `DbUpdateConcurrencyException` to 409 `quote.concurrent_modification` through the AD-8 middleware. Roughly an hour of work and it makes AD-3's rigour uniform instead of exceptional.
- Acceptable alternative: state under Deferred that the demo is single-actor, that only the Accept invariant is concurrency-safe, and name the token-plus-`If-Match` design as the revisit. A stated, reasoned deferral reads as senior judgement; an unmentioned gap reads as an oversight.

#### H3 — AD-4 publishes every domain event, contradicting the memlog, and its Prevents claim is misattributed

**Where:** AD-4, AD-5, memlog "the queue carries integration events for effects OUTSIDE the consistency boundary only".

**Problem.** Two defects:

1. AD-4's Rule says aggregates raise domain events and the interceptor "serialises **them**" into `OutboxMessages`, which the dispatcher hands to `IIntegrationEventPublisher`. Read literally, every domain event becomes a broker message. The memlog decided the opposite: the queue carries only out-of-boundary integration events. The domain-event-to-integration-event mapping — which events cross the process boundary, and under what names and payload shapes — is decided nowhere. Story authors will split: one publishes everything, another curates.
2. AD-4's **Prevents** field claims the outbox prevents "the two failure modes that make an event-driven audit trail untrustworthy." But AD-5 establishes that audit is a transactional projection that does not depend on the outbox at all. The stated justification for the spine's most expensive piece of machinery does not describe what that machinery actually protects.

**Suggested fix.**

- Amend AD-4: "The interceptor writes **all** domain events to `AuditEntry` per AD-5. It writes to `OutboxMessages` **only** those domain events present in an explicit integration-event allow-list, which lives in one file in `Application`. A domain event absent from that list never leaves the process. Integration-event payloads are versioned contracts and never expose Domain types directly."
- Rewrite Prevents to what is actually true: it guarantees that external side effects (notification dispatch, future system sync) happen exactly when and only when the state change committed — no lost effects on crash, no phantom effects on rollback.

#### H4 — Whole dimension effectively silent in the spine: migration, seeding, and the fresh-clone run contract

**Where:** Source tree, Consistency Conventions, Deployment section. Decided in the memlog only.

**Problem.** The memlog decides "migrations committed and applied on start-up with idempotent seeding." The spine — which is the build substrate the story layer reads — never says it. Nothing in the spine states that migrations are committed, that `Database.MigrateAsync()` runs at start-up, what the seeder produces, what the demo credentials are, where the SQLite file lives, or whether it is gitignored. A decision that exists only in the memlog will be re-decided at story time, and the two most likely wrong turns (`EnsureCreated()`, which silently skips the migration history and therefore AD-3's filtered index; or no seeding, leaving a reviewer at a login screen with no valid credentials) both defeat the hard "must run from a fresh clone" constraint.

Seeding is also load-bearing for FR-4: a triage dashboard with an empty database demonstrates nothing. The seed must produce quotes positioned in each attention state.

**Suggested fix.** Promote to an AD or a conventions block, stating: migrations are committed and are the only schema authority (`EnsureCreated` is banned); `MigrateAsync` plus an idempotent seeder run at start-up guarded to Development and Demo environments; the seeder creates one user per role with documented credentials, two client orgs, two vendor orgs, and requests whose quotes sit in every lifecycle state including one near expiry and one with several competing quotes; the database file path and its gitignore entry; and the exact fresh-clone command sequence, which must be the same sequence the README publishes.

#### H5 — Structural seed carries no attributes, and `Request`'s lifecycle is referenced but undecided

**Where:** "Core entities" ER diagram, AD-3 ("sets the parent `Request` to `Awarded`").

**Problem.** The ER diagram gives attributes for `OUTBOX_MESSAGE` and `AUDIT_ENTRY` — the two infrastructure tables — and none for `ORGANIZATION`, `REQUEST`, `QUOTE`, or `APP_USER`, the four entities every FR-1 story touches. Consequences: the money convention (`decimal(18,2)` plus ISO-4217 code) exists in the conventions table but never appears as columns, so it will be applied inconsistently; `ExpiresAt` is implied by AD-2's `Expire` action and AD-11's "proximity to expiry" but exists nowhere; `Quote.Status` and the vendor-versus-client organisation foreign keys are unnamed.

Separately, AD-3 sets `Request.Awarded`, so `Request` has a status — but its status set and transition rules are never stated, while `Quote`'s are given an entire AD and a state diagram. The FR-1 and FR-2 stories will invent a `Request` status enum, and they will invent different ones.

**Suggested fix.** Add attributes to the ER diagram for all four entities, including at minimum: `Quote { Status, Amount decimal(18,2), CurrencyCode, ExpiresAt, VendorOrganizationId, RequestId, Version }`, `Request { Status, ClientOrganizationId, Title, NeededBy, Version }`, `Organization { Name, Kind }`, `AppUser { DisplayName, PasswordHash, Roles }`. Then decide `Request` status in one line: either a small explicit set (`Open`, `Awarded`, `Cancelled`) with a stated rule that it changes only as a consequence of quote transitions inside the same transaction, or state that it is derived from its quotes and stored nowhere. Either is fine; leaving it half-referenced is not.

#### H6 — Nothing governs field mutability by status, so FR-1 "manage" will diverge from FR-3 enforcement

**Where:** AD-2 (status transitions only), FR-1 row of the capability map.

**Problem.** AD-2 is authoritative over *status* changes and says nothing about *data* changes. So: can a vendor edit a quote's amount after submitting it? Can a requester change a request's scope after quotes arrive? Can a quote be deleted, or only withdrawn? These are FR-1 questions with FR-3 consequences — an editable-after-submit amount makes the whole review lifecycle meaningless — and each will be answered independently by whichever story touches the endpoint first. The UI will then diverge again, because AD-7 gives it no capability flag to render an edit control from, which pushes a frontend developer straight into the `status`-branching that AD-7 forbids.

**Suggested fix.** Extend AD-2's Rule: "A quote's business fields are mutable only in `Draft`; a request's only while it has no submitted quotes. Every other edit is rejected as a domain violation with code `quote.not_editable` / `request.not_editable`. Nothing is hard-deleted — removal is expressed as a lifecycle action (`Withdraw`) so the audit trail stays complete." Then extend AD-7 so `permittedActions` carries non-transition capabilities (`Edit`, `Delete`, `AddQuote`) alongside transitions, keeping the client's single source of truth genuinely single.

#### H7 — The list-query request contract is undecided while the response envelope is decided

**Where:** Consistency Conventions, HTTP surface row; AD-11.

**Problem.** The response envelope is pinned (`items`, `page`, `pageSize`, `total`) — good, and it correctly forbids bare arrays. The request side is entirely open: parameter names (`page`/`pageNumber`/`offset`, `sort`/`orderBy`), whether `page` is 0- or 1-based, sort-direction syntax, the default and maximum page size, and the filter vocabulary (`status`, `organizationId`, `expiringWithinDays`). The spine mandates several independent list surfaces — requests, quotes, the audit timeline, and at least one dashboard view — so these will be built by different stories and will not match. The generated-client-per-resource convention then hardens each variant into its own TypeScript module.

**Suggested fix.** Pin one contract in the conventions table: `?page=1&pageSize=25&sort=-createdAt&<filter>=<value>`, `page` 1-based, `pageSize` default 25 and maximum 100 (clamped, not rejected), sort direction by leading `-`, and each endpoint declaring a closed allow-list of sortable fields (rejecting anything else 400 rather than ignoring it). Mandate one shared `PagedQuery` binding type and one `PagedResult<T>` used by every list endpoint including the audit timeline. Add one line stating that offset paging is deliberate at these data volumes and keyset paging is the revisit.

#### H8 — The mandated scope is not deliverable in two days including rehearsal, and no ordering is stated

**Where:** whole spine; AD-6 in particular; Deferred table.

**Problem.** Judged against the stated pass condition, this is the finding most likely to decide the outcome. The spine mandates, as non-optional: four source projects plus a SPA; three test projects; a transactional outbox with a `SaveChanges` interceptor; a dispatcher `BackgroundService`; an in-process channel consumer with idempotency; an expiry sweep `BackgroundService`; **two adapters for four ports, each pair held to a shared contract test suite** (AD-6); Serilog **and** OpenTelemetry **and** an Azure Monitor exporter; ASP.NET Core Identity primitives with JWT and a fallback policy; RFC 9457 middleware with a closed code set; architecture tests; FluentValidation across the API edge; dashboard projections and an audit timeline; OpenAPI plus Scalar plus a generated TypeScript client per resource; a Dockerfile and compose file; a CI workflow building and testing both stacks; and a Mantine SPA covering login, organisations, requests, quotes, a transition surface, a triage dashboard, and a timeline.

The Deferred table defers nine things, but nothing on it is a *build-effort* reduction inside the load-bearing machinery — the two heaviest mandates (AD-6's doubled adapters with shared contract tests, and the Serilog-plus-OTel-plus-Azure-Monitor triple) survive untouched. And no section states what must exist for the demo to happen versus what is stretch, so a story-layer planner has no basis for sequencing under time pressure and will discover the shortfall on day two.

**Suggested fix.** Two changes, both cheap:

1. Cut inside the mandates. Reduce AD-6 to one genuinely dual-adapter port — messaging, which is the candidate's stated demo intent — with telemetry expressed as an OTel exporter swap (already nearly free) and notification as a single local adapter behind the port. Drop the shared-contract-test mandate to what C2 describes. Fold `QuoteManager.Architecture.Tests` into `QuoteManager.Domain.Tests` as a single test class. Make `Expire` a manual action plus a stored `ExpiresAt` that projections read, and defer the sweep `BackgroundService` — the state is visible in the dashboard either way, and this removes an entire hosted service and its transactional and idempotency questions. Choose one log pipeline, not two.
2. Add a short "Delivery order" section to the spine naming the demo-critical spine (auth, entities, transitions with `permittedActions`, audit, triage dashboard), then the differentiators (outbox plus Service Bus adapter, OTel), then the artifacts (Dockerfile, CI). State plainly that the Dockerfile and compose file are reviewable artifacts only — the spine already says the demo never runs them, which is a good call worth keeping visible.

---

### MEDIUM

#### M1 — Secret handling is internally contradictory, and the signing key has no constraints

**Where:** Consistency Conventions, Configuration row; AD-9.

**Problem.** The convention reads "no secret is ever committed, and the development signing key lives only in `appsettings.Development.json`" — but `appsettings.Development.json` is a committed file, so the rule contradicts itself in a single sentence. Worse, the fresh-clone constraint actively *requires* a committed development key, otherwise a reviewer cannot start the app. Separately, AD-9 pins HS256 but sets no key-length floor; `JwtBearer` throws at signing time on keys shorter than 256 bits, which is a plausible day-two surprise. Token lifetime is characterised as "short" in Deferred but never given a value. The environment set is never enumerated.

**Suggested fix.** Restate honestly: "Two environments, Development and Production. The development signing key is committed in `appsettings.Development.json` so a fresh clone runs with no setup; it is clearly labelled as a non-production value and is at least 32 bytes. In Production the key is supplied by environment variable or Key Vault and start-up fails fast if absent. Access-token lifetime is 8 hours. No other secret is committed in any environment." That reads as a deliberate trade-off; the current wording reads as an unnoticed one.

#### M2 — Enum persistence is unspecified, and AD-3's own rule text depends on it

**Where:** AD-3 ("rows where `Status = 'Accepted'`"), Consistency Conventions.

**Problem.** AD-3's filtered-index rule quotes a string comparison, which only works if `QuoteStatus` is persisted as text. Nothing decides that. If the EF configuration story maps enums to `int` (the EF Core default), the migration's `HasFilter("Status = 'Accepted'")` silently matches nothing on SQLite, and the database-level guarantee that AD-3 leans on evaporates without any visible failure — the aggregate check still passes, so tests pass, and the invariant is quietly unprotected under concurrency.

**Suggested fix.** Add a conventions row: "Enums persist as strings via `HasConversion<string>()` with an explicit max length, so data is readable in the SQLite file and index filter predicates are stable." Then add an integration test that inserts two accepted siblings directly through the `DbContext`, bypassing the aggregate, and asserts the unique-index violation. That test is the only thing that actually proves AD-3's second guard exists.

#### M3 — Health and readiness endpoints are implied by AD-9 but specified nowhere

**Where:** AD-9 ("`AllowAnonymous` on login and health only").

**Problem.** "Health" is referenced as an exception to the auth policy but is not a decided dimension: no paths, no liveness-versus-readiness split, no registered checks, no statement of whether readiness includes the database or the outbox backlog. The brief's role constraints call out observability and production readiness explicitly, so this is also a scored surface, not just an internal gap.

**Suggested fix.** One conventions row: `/health/live` returns process liveness with no dependency checks; `/health/ready` includes a database connectivity check and, once the outbox exists, a backlog-threshold check. Both anonymous. Register via `AddHealthChecks` and `MapHealthChecks`.

#### M4 — SQLite write-concurrency posture is unstated while a background service writes concurrently with request handlers

**Where:** Stack (SQLite), AD-4 (dispatcher `BackgroundService`), deployment diagram.

**Problem.** SQLite permits one writer. The outbox dispatcher marks rows dispatched on a polling loop while request handlers commit transactions, and the expiry sweep adds a third writer. Nothing states journal mode, busy timeout, or pooling posture. `SQLITE_BUSY` surfacing as a 500 during a live demo is a realistic outcome, and it is the kind of failure that looks like carelessness rather than bad luck.

**Suggested fix.** Add to the conventions or the deployment section: the connection string sets `Cache=Shared` and WAL journal mode is enabled at start-up; `DefaultTimeout` is set explicitly; the dispatcher polls on an interval with jitter rather than tight-looping, and takes a short transaction per batch. State the single-writer property as a known property of the provider choice with a cross-reference to the deferred scale-out row.

#### M5 — Three sources imply three different identity storage models

**Where:** Stack (`Microsoft.AspNetCore.Identity.EntityFrameworkCore`), ER diagram (`APP_USER`), memlog (`AddIdentityCore` plus `PasswordHasher`).

**Problem.** The EF Identity package implies `IdentityDbContext` and its seven-table schema (`AspNetUsers`, `AspNetRoles`, ...). The ER diagram shows a custom `APP_USER`. The memlog says only `AddIdentityCore` and `PasswordHasher` are used, which needs just `Microsoft.Extensions.Identity.Core`. A story author will pick one, and the choice materially changes the schema, the seeder, and how AD-10's `ICurrentUser` resolves roles.

**Suggested fix.** Decide, and align all three. For this timebox the lean option is right: reference `Microsoft.Extensions.Identity.Core` only, hash with `PasswordHasher<AppUser>`, own a hand-written `AppUser` and `AppUserRole` in the Domain, and drop the EF Identity package from the Stack table. Note in one line that full ASP.NET Core Identity is the upgrade path — which is the same argument the Deferred table already makes about refresh tokens.

#### M6 — Stack omissions, including the one realistic version-resolution hazard

**Where:** Stack table.

**Problem.** Four things the spine mandates have no package or version:

- The **architecture-test library** AD-1 depends on for its "fails the build" claim.
- The **OpenTelemetry core packages** the local console-exporter profile requires. This is the one genuine "cannot resolve together" risk in the stack: `Azure.Monitor.OpenTelemetry.AspNetCore` 1.6.0 pins OTel core transitively, and unpinned direct references to `OpenTelemetry.Extensions.Hosting` / `.Instrumentation.AspNetCore` / `.Exporter.Console` produce a diamond that fails restore or, worse, resolves to a mismatched pair that no-ops instrumentation.
- The **OpenAPI-to-TypeScript generator** required by "one generated API client module per resource" — plus whether generation is a build step or committed output, which is itself a divergence point.
- **ESLint and typescript-eslint**, verified in the memlog (10.8.0 / 8.65.0) and dropped from the Stack, despite the TypeScript 6 pinning decision being justified *by* typescript-eslint compatibility.

**Suggested fix.** Add all four rows with versions. Pin the OTel core packages explicitly to the versions `Azure.Monitor.OpenTelemetry.AspNetCore` 1.6.0 resolves, and add a `Directory.Packages.props` with central package management so the pin is expressed once — worth the ten minutes on a four-project solution and it reads as production-minded. State whether the TypeScript client is generated at build time or committed; committed is the better call here, because a reviewer cloning fresh should not need the API running to build the SPA.

#### M7 — AD-11 hands the story author a choice, and FR-4's dashboard contract is undefined

**Where:** AD-11 ("computed in the projection **or** from stored columns"), FR-4 row of the capability map.

**Problem.** FR-4 is the brief's genuine differentiator and the spine treats it seriously, which is to its credit. But AD-11 offers two implementation routes for attention signals rather than deciding one, which is the opposite of what a spine does — and the two routes have different consequences, since stored columns need write-path maintenance and go stale, while projected computation cannot be indexed. Beyond that, the dashboard's actual contract is undefined: which views exist ("awaiting my review", "expiring soon", "stale"), what thresholds make a quote stale or a request neglected, whether signals are returned as raw values or pre-bucketed severities, and how the UI renders them. Two stories will produce two dashboards.

**Suggested fix.** Decide: all attention signals are **computed in the projection** against the injected `TimeProvider`; no denormalised signal columns exist (removing the staleness class of bug entirely and keeping the "deterministic under test" property AD-11 already claims). Then pin the contract: name the three or four dashboard views, give each signal a numeric threshold in configuration-bound options rather than a magic number, and return a `severity` enum alongside the raw value so the UI maps severity to colour without re-implementing the thresholds — the same discipline AD-7 applies to actions, applied to signals.

#### M8 — AD-8's closed error-code set is asserted but never enumerated, and the client half is unenforceable

**Where:** AD-8.

**Problem.** AD-8 requires a `code` "drawn from a closed set of stable identifiers" and requires the UI to branch on `code` only. Neither half is checkable: the set is never enumerated and no location owns it, and nothing says where the client's code-to-message mapping lives. With no catalogue, each component will inline its own copy for the codes it happens to encounter, and the UI will end up as inconsistent as the string-matching AD-8 was written to prevent — just one layer further in.

**Suggested fix.** Enumerate the initial set in AD-8 or a companion (`quote.already_accepted`, `quote.invalid_transition`, `quote.not_editable`, `quote.concurrent_modification`, `request.not_found`, `auth.invalid_credentials`, `validation.failed`, `authz.forbidden`), state that it lives in one `ErrorCodes` static class in `Domain` and that every typed domain exception carries one, and require exactly one client-side `errorCatalog` mapping code to user-facing copy with a defined fallback for unknown codes. Add a test asserting every typed domain exception maps to a code in the enumerated set.

#### M9 — The expiry mechanism is referenced in three places and governed in none

**Where:** paradigm diagram ("expiry sweep"), AD-2 (`Expire` action), AD-11 ("proximity to expiry").

**Problem.** Expiry appears as a hosted service in the layer diagram, as a transition in the state machine, and as an attention signal in AD-11, but no AD decides who sets `ExpiresAt`, whether it is required, what cadence the sweep runs at, whether it is transactional, whether it is idempotent if it overlaps its own previous run, or which actor its audit rows carry. AD-10's system-actor clause covers the last point, which is good — everything else is open. The three stories that touch expiry will each assume something different.

**Suggested fix.** Either govern it in one paragraph under AD-2 (ExpiresAt is set at quote creation from a configured default offset; the sweep runs on a stated interval, applies `Expire` through `ApplyQuoteAction` under the system actor, processes one quote per transaction, and is naturally idempotent because `Expire` is illegal from a terminal state) or take the H8 cut and remove the sweep entirely, keeping `ExpiresAt` as data the projections read and `Expire` as a manual action. The second is cheaper and loses nothing demonstrable.

#### M10 — The bundled-npm absolute-path workaround must be quarantined from committed artifacts

**Where:** Stack, Node row ("npm 10.9.3, invoked by absolute path"); memlog constraint.

**Problem.** The workaround is correct for the author's machine — a stale global npm 8.3.2 shadowing the bundled 10.9.3 — and the reasoning not to mutate the user's global install is sound. But the Stack table states it as a project property, and any absolute path that reaches `package.json` scripts, the CI workflow, or the README breaks on the reviewer's machine, where that path does not exist. That directly defeats the "runs from a fresh clone" constraint, and the spine currently gives no rule preventing the leak.

**Suggested fix.** Add one line: the absolute-path invocation is a local developer workaround and must not appear in `package.json`, the CI workflow, the Dockerfile, or the README, all of which use plain `npm`. Commit `package-lock.json` and pin an `engines` field so the reviewer's version mismatch surfaces as a clear message rather than a strange build failure.

#### M11 — Nothing binds what the CI gate enforces, though AD-1 depends on it

**Where:** source tree (`.github/workflows/ci.yml`), AD-1.

**Problem.** AD-1 claims its rule "fails the build rather than relying on review." That is only true if the architecture-test project runs in the default `dotnet test` invocation and CI actually runs it. The workflow appears in the source tree with a comment and no governing rule, so nothing states what must pass. An AD whose enforcement mechanism is optional is enforced by review, which is what AD-1 explicitly disclaims.

**Suggested fix.** Add a conventions row stating the gate: restore, build with warnings-as-errors on the four source projects, `dotnet test` across all test projects (including the architecture tests, which must not be trait-excluded), `tsc --noEmit`, ESLint, and the SPA production build. State that a red gate blocks merge. This is also the right place to state that `[Trait("Requires","Azure")]` tests are excluded from the default filter, per C2.

#### M12 — End-to-end tests are deferred entirely, against an explicit role requirement

**Where:** Deferred, Playwright row.

**Problem.** The reasoning is honest and the cost-per-hour argument is correct for a two-day build. But the brief lists "automated unit, integration, and end-to-end tests" as required by the role, so a wholly absent tier is a visible gap in a submission being scored on breadth. The Deferred entry already gestures at the right answer ("starting with one login-to-accept smoke path") but leaves it conditional on spare time, which in a two-day build means it will not happen.

**Suggested fix.** Move exactly one Playwright spec — log in, open a request, accept a quote, assert the sibling quotes show as superseded and the timeline shows the entries — from Deferred into scope, and keep the rest of the suite deferred with the current reasoning. One spec demonstrates the capability and the wiring; the argument for not building fifty is then a strength rather than an omission.

---

### LOW

#### L1 — Two logging pipelines, no statement of which owns what

Serilog (with console and file sinks) and OpenTelemetry logging are both configured, and the conventions require every entry enriched with trace id and user id, but the mechanism is unnamed (Serilog enricher versus the OTel logging bridge) and neither is declared the owner. The realistic outcomes are duplicated log output or an OTel log pipeline that receives nothing. **Fix:** state that Serilog owns application logging with `TraceId`/`SpanId`/`UserId` enrichers, that OTel owns traces and metrics, and — if logs should reach Azure Monitor — that a single Serilog OTel sink is the bridge. Or drop Serilog entirely and use OTel logging with `ILogger`, which is the smaller build (see H8).

#### L2 — The UUIDv7 rationale does not apply to the chosen provider

The identifiers convention justifies `Guid.CreateVersion7()` as avoiding clustered-index fragmentation. SQLite has no clustered indexes; a `Guid` primary key is stored as a BLOB or TEXT in a rowid table. The decision is still right (monotonic ids give free insertion ordering, which AD-4's dispatcher actually relies on), but the stated reason is wrong for this stack, and an interviewer reading closely may notice. **Fix:** rejustify on insertion-order semantics and future-provider portability, and cross-reference AD-4's "insertion order" claim, which currently has no stated basis.

#### L3 — Terminal-state finality is not stated

The state diagram routes `Accepted`, `Rejected`, `Withdrawn`, and `Expired` to `[*]`, implying no reopen path, and AD-2's table would reject one — but the prose never says so, and "can a rejected quote be resubmitted?" is a natural stakeholder question that a story author may answer by adding a table row. **Fix:** one sentence — terminal states are final in this build; a superseded need is expressed as a new quote, and reopening is not a supported transition.

#### L4 — AD-7 and AD-8's client-side halves are enforced by review only

"No TypeScript may branch on `status`" and "the UI branches on `code` only" are the two rules most likely to be violated under time pressure, and both are prose. **Fix:** add an ESLint `no-restricted-syntax` rule flagging member access on `status` inside conditional expressions in component files, plus `no-restricted-imports` blocking direct `fetch`/`axios` outside the single `apiClient` (which also enforces C3). Two lint rules convert three aspirational sentences into build failures.

#### L5 — `companions: []` while the memlog holds an open question about renderings

The front matter declares no companions and `status: draft`, while the memlog carries an unresolved question about an additional human-facing rendering for the interview. That is correctly parked, not missing — but the spine's own front matter should name it as an open question rather than leaving the reader to infer completeness from an empty array. **Fix:** add an `open_questions` key naming the deferred rendering decision, so the spine is self-describing about its own incompleteness.

---

## What Is Genuinely Strong

Stated so the revision does not sand off what is working:

- **AD-7 is the best decision in the spine.** Projecting `permittedActions` from server truth is the correct answer to the highest-probability divergence in a split full-stack build, and the Prevents field identifies that risk accurately.
- **AD-3's belt-and-braces guard** — aggregate check plus filtered unique index plus a specified 409 mapping rather than a 500 — is the level of rigour the rest of the spine should match, and H2 exists only because it does not yet.
- **AD-5's audit-is-not-logging distinction** correctly reinterprets an ambiguous stated intent ("logging for tracking client actions") as an audit requirement. That is a senior reading of the brief, not a literal one.
- **AD-6's core insight** — the demo path and the deployed path must be the same code, and a missing cloud resource must be a supported configuration rather than a start-up crash — is exactly right for the stated Azure constraint. C2 attacks its overreach and its unexecutable verification clause, not its intent.
- **The Docker decision** (ships as a reviewable artifact, never on the demo critical path) shows the right instinct about what a live demo can afford.
- **The stack pinning discipline** is unusually careful: the TypeScript 6-over-7 rationale, the Shouldly-over-FluentAssertions licensing call, and the OTel-distro-over-classic-App-Insights choice are all correct and all justified from primary sources.
