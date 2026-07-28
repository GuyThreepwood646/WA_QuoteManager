---
review: version-and-reality-check
target: ARCHITECTURE-SPINE.md
artifact: architecture-QuoteManager-2026-07-28
reviewer_lens: 'Version and Reality Check (mandatory Reviewer Gate lens)'
date: '2026-07-28'
verdict: 'PASS WITH TWO REQUIRED CORRECTIONS'
---

# Version and Reality-Check Review — ARCHITECTURE-SPINE.md (QuoteManager)

## Method

Every version in the `## Stack` table was resolved against a primary registry on 2026-07-28, not from recall:

- NuGet: `https://api.nuget.org/v3-flatcontainer/<lowercase-id>/index.json` for the full version list, and `.../<id>/<version>/<id>.nuspec` where dependency or target-framework detail was needed.
- npm: `https://registry.npmjs.org/<package>` and `.../latest` for `version`, `dist-tags`, `engines`, `peerDependencies`, `peerDependenciesMeta`, and publish timestamps.
- Node.js: `https://nodejs.org/dist/index.json` for release, LTS codename, and bundled npm version.
- .NET: `https://raw.githubusercontent.com/dotnet/core/main/release-notes/releases-index.json` for channel, release type, support phase, and EOL date.
- Feature and API claims: Microsoft Learn API/conceptual pages, the SQLite documentation, the RFC Editor, xunit.net documentation, and the FluentAssertions releases page.

The headline finding is that this table is unusually accurate. Twenty-six of twenty-eight version pins are the exact current stable release. The two problems are not fabrications, they are a stale pin and a missing package, and both are one-line fixes. Neither invalidates any architectural decision.

## Summary of findings

| # | Severity | Finding |
| --- | --- | --- |
| VR-1 | **Critical** | Node.js 22.18.0 is below the `engines` floor that `react-router` 8.3.0 declares (`>=22.22.0`). The pinned Node is also a year stale, and the paired npm version is a consequence of that staleness. |
| VR-2 | **Critical** | `Microsoft.NET.Test.Sdk` 18.8.1 is pinned without `xunit.runner.visualstudio`. xUnit's own documentation states VSTest mode requires both. As specified, `dotnet test` discovers zero tests unless `global.json` opts into Microsoft Testing Platform, which the spine's `global.json` comment does not mention. |
| VR-3 | Minor | "React Router 8.3.0" does not name a package id. `react-router` has 8.3.0; `react-router-dom` tops out at 7.18.1 and has no 8.x. A builder who reaches for the historically conventional package gets an unresolvable version. |
| VR-4 | Minor | The conventions make deterministic time a rule (`TimeProvider`, `DateTime.Now` banned, "deterministic under test"), but no fake-clock package appears in the stack. `Microsoft.Extensions.TimeProvider.Testing` 10.8.0 is the first-party `FakeTimeProvider`. |
| VR-5 | Advisory | Every remaining technology claim in the document was independently confirmed, including the four that would have invalidated a decision if wrong (FluentAssertions licensing, `Guid.CreateVersion7`, SQLite partial unique indexes, TypeScript 7's missing programmatic API). No architectural decision needs revisiting. |

## Part 1 — Every version claim

Legend: **OK** = exists and is the current stable release. **STALE** = exists but is not current. **WRONG** = does not exist or is misidentified.

### .NET platform

| Claim | Claimed | Actual (2026-07-28) | Verdict | Source consulted |
| --- | --- | --- | --- | --- |
| .NET SDK | 10.0.302 | `latest-sdk` for channel 10.0 is exactly `10.0.302` | **OK** | dotnet/core `releases-index.json` |
| Target framework | `net10.0` | Channel 10.0 is the current LTS, `release-type: lts`, `support-phase: active` | **OK** | dotnet/core `releases-index.json` |
| .NET 10 is LTS | LTS | Confirmed `lts`. 8.0 is also LTS but in `maintenance` (EOL 2026-11-10); 9.0 is STS in maintenance; 11.0 exists only as `preview` | **OK** | dotnet/core `releases-index.json` |
| Support end date | 2028-11-14 | `eol-date: 2028-11-14` — exact match | **OK** | dotnet/core `releases-index.json` |
| ASP.NET Core | 10.0.10 | `latest-release` for 10.0 is `10.0.10`; `Microsoft.AspNetCore.App.Ref` newest stable is 10.0.10 (newest of any kind is `11.0.0-preview.6.26359.118`) | **OK** | dotnet/core index; NuGet flat container |

`net10.0` and SDK 10.0.302 are coherent: SDK 10.0.302 is the 10.0 channel SDK that ships runtime 10.0.10, and `rollForward: latestMinor` in `global.json` is a sane band for a build with a two-day timebox.

### NuGet packages

| Claim | Claimed | Actual latest stable | Verdict | Source consulted |
| --- | --- | --- | --- | --- |
| Microsoft.EntityFrameworkCore | 10.0.10 | 10.0.10 | **OK** | NuGet flat container |
| Microsoft.EntityFrameworkCore.Sqlite | 10.0.10 | 10.0.10 | **OK** | NuGet flat container |
| Microsoft.EntityFrameworkCore.Design | 10.0.10 | 10.0.10 | **OK** | NuGet flat container |
| Microsoft.Data.Sqlite | 10.0.10 | 10.0.10 | **OK** | NuGet flat container |
| Serilog.AspNetCore | 10.0.0 | 10.0.0; nuspec ships a `net10.0` asset group | **OK** | NuGet flat container + nuspec |
| Serilog.Sinks.Console | 6.1.1 | 6.1.1 | **OK** | NuGet flat container |
| Serilog.Sinks.File | 7.0.0 | 7.0.0. Newest *any* is `8.0.0-nblumhardt-02322`, a prerelease — correctly not taken | **OK** | NuGet flat container |
| Azure.Monitor.OpenTelemetry.AspNetCore | 1.6.0 | 1.6.0; nuspec ships a `net10.0` asset group | **OK** | NuGet flat container + nuspec |
| Azure.Messaging.ServiceBus | 7.20.2 | 7.20.2 | **OK** | NuGet flat container |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.10 | 10.0.10 | **OK** | NuGet flat container |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.10 | 10.0.10 | **OK** | NuGet flat container |
| Microsoft.AspNetCore.OpenApi | 10.0.10 | 10.0.10 | **OK** | NuGet flat container |
| Scalar.AspNetCore | 2.16.16 | 2.16.16; nuspec ships a `net10.0` asset group | **OK** | NuGet flat container + nuspec |
| FluentValidation | 12.1.1 | 12.1.1 | **OK** | NuGet flat container |
| FluentValidation.DependencyInjectionExtensions | 12.1.1 | 12.1.1 — the two packages are version-locked, so a single row is correct | **OK** | NuGet flat container |
| xunit.v3 | 3.2.2 | 3.2.2. Newest any is `4.0.0-pre.154`, prerelease — correctly not taken | **OK** | NuGet flat container |
| Microsoft.NET.Test.Sdk | 18.8.1 | 18.8.1 | **OK** as a version; see **VR-2** for the composition problem | NuGet flat container + nuspec |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.10 | 10.0.10 | **OK** | NuGet flat container |
| Shouldly | 4.3.0 | 4.3.0. Newest any is `5.0.0-preview.2`, prerelease — correctly not taken | **OK** | NuGet flat container |
| NSubstitute | 6.0.0 | 6.0.0 | **OK** | NuGet flat container |

### Node and npm packages

| Claim | Claimed | Actual latest | Verdict | Source consulted |
| --- | --- | --- | --- | --- |
| Node.js | 22.18.0 | 22.18.0 exists (released 2025-07-31, LTS "Jod"). Current 22.x is **22.23.1** (2026-06-22) | **STALE — see VR-1** | `nodejs.org/dist/index.json` |
| npm | 10.9.3 | Correct *for* Node 22.18.0. Node 22.23.1 bundles npm **10.9.8** | **STALE (consequential)** | `nodejs.org/dist/index.json` |
| React | 19.2.8 | 19.2.8 | **OK** | npm registry |
| React DOM | 19.2.8 | 19.2.8; peer `react=^19.2.8` — exact-pair match | **OK** | npm registry |
| TypeScript | 6.0.3 | Real, published 2026-04-16, and the newest 6.x stable (only 6.0.2 and 6.0.3 exist). `dist-tags.latest` is 7.0.2 | **OK — deliberate, and correct; see Part 3** | npm registry |
| Vite | 8.1.5 | 8.1.5 | **OK** | npm registry |
| @vitejs/plugin-react | 6.0.4 | 6.0.4 | **OK** | npm registry |
| @mantine/core | 9.5.0 | 9.5.0 | **OK** | npm registry |
| @mantine/hooks | 9.5.0 | 9.5.0 | **OK** | npm registry |
| @mantine/form | 9.5.0 | 9.5.0 | **OK** | npm registry |
| @mantine/dates | 9.5.0 | 9.5.0 | **OK** | npm registry |
| @tanstack/react-query | 5.101.4 | 5.101.4 | **OK** | npm registry |
| React Router | 8.3.0 | `react-router` 8.3.0 exists (published 2026-07-22, latest). `react-router-dom` has no 8.x — latest is 7.18.1 | **OK for `react-router`; see VR-3** | npm registry |

## Part 2 — Technology existence and fitness

Each of these was called out in the review mandate because a wrong answer would invalidate a committed decision. All eight were confirmed.

### .NET 10 LTS coherence with `net10.0` and SDK 10.0.302 — CONFIRMED

Channel 10.0 is `release-type: lts`, `support-phase: active`, `eol-date: 2028-11-14`, `latest-release: 10.0.10`, `latest-sdk: 10.0.302`. The spine's four claims match the release index field for field. .NET 8 is the only other LTS and is already in maintenance with an EOL inside 2026, so 10.0 is the only defensible LTS choice today.

### `Microsoft.AspNetCore.OpenApi` + `Scalar.AspNetCore`, and Swashbuckle no longer the default — CONFIRMED

Microsoft's own OpenAPI page states: "Starting with .NET 9, ASP.NET Core includes built-in OpenAPI support. The `Microsoft.AspNetCore.OpenApi` package provides OpenAPI document generation at runtime," and, decisively, "ASP.NET Core generates OpenAPI documents only. Interactive UIs such as **Swagger UI** or **Scalar** are not included by default and must be added separately." That is exactly the shape the spine adopts: framework document generation plus a separately chosen UI.

One nuance worth stating plainly so the decision is not defended on a false premise. Swashbuckle is *not* abandoned — `Swashbuckle.AspNetCore` 10.2.3 is current and actively released. The correct justification is "no longer the templated default, and Scalar is a documented first-class alternative," which is what the spine claims. Do not upgrade that into "Swashbuckle is dead."

### Classic Application Insights SDK superseded by `Azure.Monitor.OpenTelemetry.AspNetCore` — CONFIRMED

The Azure Monitor OpenTelemetry enablement page lists `Azure.Monitor.OpenTelemetry.AspNetCore` as *the* ASP.NET Core package, with `dotnet add package Azure.Monitor.OpenTelemetry.AspNetCore` as the install step, and repeatedly directs readers off the old SDK: "If you're migrating from older Application Insights SDKs, see our migration documentation." The classic `Microsoft.ApplicationInsights.AspNetCore` package still publishes (3.1.2 is current), so it exists — but it is the migrate-away path, not the recommended path for a new app. Version 1.6.0 is current and ships a `net10.0` asset group. The decision holds.

### FluentAssertions 8+ requires a paid commercial licence — CONFIRMED, so the avoidance decision is correct

The FluentAssertions releases page, under "License Change," states: "Versions 8 and beyond are/will be free for open-source projects and non-commercial use, but commercial use requires a paid license." A later entry adds "Provide a toggle to suppress the soft warning that commercial use requires a paid license (#2984)," which corroborates that the runtime actually emits a licensing warning. Current FluentAssertions is 8.10.0, well inside the paid band.

This is the single claim most likely to have been an out-of-date rumour, and it is true. The spine's choice of Shouldly 4.3.0 instead is therefore correctly motivated, not superstition. Shouldly 4.3.0 is Apache-licensed and current.

### `Guid.CreateVersion7()` exists in the targeted .NET — CONFIRMED

Microsoft Learn lists `Guid.CreateVersion7()` and `Guid.CreateVersion7(DateTimeOffset)` with **Applies to: .NET 9, 10, 11**. `net10.0` is in range. Both overloads create a `Guid` per RFC 9562 Version 7, which is what the conventions table describes. The monotonic-sort rationale is sound. Note the parameterless overload's documented remark — "This method uses `UtcNow` to determine the Unix Epoch timestamp source" — which means it bypasses the injected `TimeProvider`. That is not a version defect, but if the team wants ids that are deterministic under test alongside the AD-11 time rule, use `Guid.CreateVersion7(timeProvider.GetUtcNow())`. The overload exists precisely for this.

### RFC 9457 is current and obsoletes RFC 7807 — CONFIRMED

The RFC Editor header for RFC 9457 reads: "RFC: 9457, **Obsoletes: 7807**, Category: Standards Track, Published: July 2023," the abstract states "This document obsoletes RFC 7807," and it carries "This is an Internet Standards Track document." There is no "Obsoleted by" or "Updated by" entry, so 9457 is still the current problem-details specification. AD-8 cites the right RFC.

### `TimeProvider` is a first-class .NET abstraction available in `net10.0` — CONFIRMED

`System.TimeProvider` is an abstract class in `System.Runtime.dll`, with monikers including `net-8.0`, `net-9.0`, `net-10.0`, and `net-11.0`. It exposes `GetUtcNow()`, `GetLocalNow()`, `LocalTimeZone`, `GetTimestamp()`, `GetElapsedTime()`, `CreateTimer()`, and the static `TimeProvider.System`. The documented test story is `FakeTimeProvider`, which derives from it — see **VR-4** for the package that carries it.

### SQLite supports filtered/partial unique indexes, and the EF Core fluent API for one — CONFIRMED, so AD-3 is safe

SQLite's partial-index documentation has a section titled "Unique Partial Indexes": "A partial index definition may include the UNIQUE keyword. If it does, then SQLite requires every entry in the index to be unique. This provides a mechanism for enforcing uniqueness across some subset of the rows in a table." Its worked example is structurally identical to AD-3 — one leader per team, enforced as `CREATE UNIQUE INDEX team_leader ON person(team_id) WHERE is_team_leader;`. Supported since SQLite 3.8.0 (2013-08-26), so there is no version risk whatsoever.

The EF Core fluent API is `HasIndex(...).IsUnique().HasFilter(<sql>)`, per the EF Core indexes documentation section "Index filter": "You can use the Fluent API to specify a filter on an index, provided as a SQL expression." For AD-3 the declaration is:

```csharp
modelBuilder.Entity<Quote>()
    .HasIndex(q => q.RequestId)
    .IsUnique()
    .HasFilter("\"Status\" = 'Accepted'");
```

Two implementation cautions, neither of which changes the decision:

1. SQLite quotes identifiers with double quotes, not SQL Server's brackets. The EF documentation example uses `"[Url] IS NOT NULL"` because it is written for SQL Server. Copying the bracket syntax verbatim produces invalid SQLite. The filter string is raw provider SQL and is not translated.
2. The filter must match the *stored* representation of `Status`. If the enum is persisted as an integer rather than converted to a string, the filter has to compare against the integer, not `'Accepted'`. AD-3 says "restricted to rows where `Status = 'Accepted'`", which silently commits the model to a string-converted enum. Worth making explicit so the index and the value converter cannot drift apart.

Also confirmed for the Deferred table: SQLite genuinely has no `SKIP LOCKED` and no row-leasing primitive, so the stated reason for deferring outbox scale-out is factually correct.

## Part 3 — Compatibility pairings

### `@vitejs/plugin-react` 6.0.4 against Vite 8.1.5 — RESOLVES

Registry `peerDependencies` for `@vitejs/plugin-react@6.0.4`:

```
vite: ^8.0.0
@rolldown/plugin-babel: ^0.1.7 || ^0.2.0
babel-plugin-react-compiler: ^1.0.0
```

`^8.0.0` admits 8.1.5. The two Babel-related peers are declared optional in `peerDependenciesMeta` (`optional: true` for both), so npm will not demand them and no `ERESOLVE` occurs. Engine floors agree as well: both `vite@8.1.5` and `@vitejs/plugin-react@6.0.4` declare `node: ^20.19.0 || >=22.12.0`, which Node 22.18.0 satisfies. This pairing is clean.

### Mantine 9.5.0 against React 19.2.8 — RESOLVES

Actual registry peer ranges:

- `@mantine/core@9.5.0`: `react ^19.2.0`, `react-dom ^19.2.0`, `@mantine/hooks 9.5.0`
- `@mantine/hooks@9.5.0`: `react ^19.2.0`
- `@mantine/form@9.5.0`: `react ^19.2.0`
- `@mantine/dates@9.5.0`: `react ^19.2.0`, `react-dom ^19.2.0`, `@mantine/core 9.5.0`, `@mantine/hooks 9.5.0`, `dayjs >=1.0.0`

React 19.2.8 satisfies `^19.2.0`, so the "Mantine 9 requires React 19.2+" claim is accurate and the pairing resolves. Two composition notes:

1. `@mantine/core` and `@mantine/dates` pin `@mantine/hooks` to the **exact** version `9.5.0`. Bumping any one Mantine package without bumping all four breaks installation. Keeping them on one table row is the right call; the constraint is exact-equality, not caret.
2. `@mantine/dates` requires a `dayjs` peer (`>=1.0.0`) that the stack table does not list. It is a real, mandatory (non-optional) peer. Add `dayjs` explicitly or `@mantine/dates` will warn or fail depending on the resolver.

### TypeScript 6.0.3 against `typescript-eslint` and Vite 8 — RESOLVES, and the stated reasoning is correct

`typescript-eslint@8.65.0` (`dist-tags.latest`) declares:

```
eslint: ^8.57.0 || ^9.0.0 || ^10.0.0
typescript: >=4.8.4 <6.1.0
```

This single field settles the whole question:

- TypeScript **6.0.3 satisfies** `>=4.8.4 <6.1.0`. The pin works.
- TypeScript **7.0.2 does not**. There is no `typescript-eslint` 9.x or 10.x published at all — the only tags are `latest=8.65.0`, `canary=8.65.1-alpha.10`, and a historical `rc-v8`. So the supported ceiling really is below TypeScript 7, with nothing in the pipeline.

The spine's Deferred entry says TypeScript 7 has been "GA since 2026-07-08." The registry publish timestamp for `typescript@7.0.2` is `2026-07-08T15:55:18.431Z` — exact. `typescript@6.0.3` was published 2026-04-16 and is the newest 6.x stable. The reasoning that TypeScript 7 "ships without the stable programmatic API, so typescript-eslint needs a compatibility shim" is corroborated by the peer range excluding 7 entirely, and deferring until 7.1 is consistent with `dist-tags.next` being `7.1.0-dev.20260728.1` — 7.1 is in active nightly development but not released.

One forward-looking caveat: the range's upper bound is `<6.1.0`, not `<7.0.0`. If TypeScript 6.1 ships before `typescript-eslint` widens the range, 6.0.3 remains the safe pin. Do not casually bump TypeScript within the 6.x line either.

Vite 8 does not type-check; it transpiles through esbuild/Rolldown and declares no `typescript` peer at all. No TypeScript-version constraint flows from Vite in either direction.

### React Router 8.3.0 against React 19 — RESOLVES on React, FAILS on Node (VR-1)

`react-router@8.3.0` declares:

```
engines: { node: ">=22.22.0" }
peerDependencies: { react: ">=19.2.7", react-dom: ">=19.2.7" }
```

React 19.2.8 satisfies `>=19.2.7`, comfortably. The React side is fine.

The **`engines` field is the failure**. The spine pins Node.js **22.18.0**, which is below the declared floor of **22.22.0**. Under default npm settings `engines` produces an `EBADENGINE` warning rather than a hard failure, so this may not stop a local install — but it will hard-fail anywhere `engine-strict=true` is set, and it will fail on CI images or package managers that enforce engines (pnpm enforces by default). Shipping a stack table whose own pins violate a declared engine floor is exactly the kind of unverified assertion this lens exists to catch.

The fix is trivial and has no downside: Node 22.18.0 was released **2025-07-31**, almost a year before this document's date. Current Node 22 LTS ("Jod") is **22.23.1**, released 2026-06-22, bundling **npm 10.9.8**. Moving to 22.23.1 clears the react-router floor, stays on the same LTS major, and keeps satisfying Vite 8's and `@vitejs/plugin-react`'s `^20.19.0 || >=22.12.0`. The npm figure must move with it — 10.9.3 is correct only for 22.18.0.

> **VR-1 remediation:** change the Node row to `24.18.0 (npm 11.x)` if a newer LTS line is acceptable ("Krypton", released 2026-06-23), or minimally to `22.23.1 (npm 10.9.8)` to stay on Jod. Do not leave 22.18.0 in place while pinning `react-router` 8.3.0.

### VR-3 — the React Router package identity

`react-router-dom` has no 8.x; its versions stop at 7.18.1, and `dist-tags.latest` is 7.18.1. All 8.x releases (8.0.0, 8.0.1, 8.1.0, 8.2.0, 8.3.0) live on the **`react-router`** package, which is consistent with the v7 consolidation of `react-router-dom` into `react-router`. The stack row reads "React Router | 8.3.0" without a package id, which invites `npm install react-router-dom@8.3.0` and an immediate `ETARGET`. Name the package explicitly as `react-router` 8.3.0.

### xunit.v3 3.2.2 against Microsoft.NET.Test.Sdk 18.8.1 — VERSIONS COMPATIBLE, COMPOSITION INCOMPLETE (VR-2)

Both versions are current stable and there is no version conflict between them. But the nuspecs and xUnit's documentation together reveal a gap.

`xunit.v3@3.2.2`'s nuspec has exactly one dependency per target framework:

```
xunit.v3.mtp-v1 [3.2.2]   (for net8.0 and net472)
```

with the description "Installing this package installs `xunit.v3.mtp-v1`." So xunit.v3 3.2.2 brings in Microsoft Testing Platform v1 support natively. `Microsoft.NET.Test.Sdk@18.8.1` is the *VSTest* MSBuild targets package (`Microsoft.TestPlatform.TestHost` + `Microsoft.CodeCoverage`, repo `microsoft/vstest`) — a different, parallel execution path.

xUnit's Microsoft Testing Platform documentation is explicit on both ends:

- "Supporting VSTest is separate from (and does not interfere with) our support for Microsoft Testing Platform" — so the two coexisting is *not* a conflict, and keeping `Microsoft.NET.Test.Sdk` is a legitimate backward-compatibility choice.
- But: "**VSTest mode requires the package references to `xunit.runner.visualstudio` and `Microsoft.NET.Test.Sdk`.**" And: "By default, xUnit.net v3 projects use VSTest when run via `dotnet test`, support for which comes from the `xunit.runner.visualstudio` package reference."

The spine pins `Microsoft.NET.Test.Sdk` 18.8.1 and does **not** pin `xunit.runner.visualstudio` (current 3.1.5). That is half of the VSTest pair. The consequence depends on how `dotnet test` is configured, and the spine's `global.json` comment mentions only "pins the SDK band, rollForward latestMinor":

- If `global.json` stays as described, `dotnet test` under SDK 10 defaults to VSTest, which has the SDK targets but no xUnit adapter — **test discovery finds nothing and CI goes green on zero tests**, the worst possible failure mode for a build whose credibility rests on `.github/workflows/ci.yml` running "restore, build, test both stacks."
- If the intent was MTP all along, then `Microsoft.NET.Test.Sdk` is dead weight and `global.json` is missing the opt-in that the xUnit docs specify for SDK 10 and later.

> **VR-2 remediation — pick one, and record it:**
>
> **Option A (VSTest, maximum tooling compatibility):** add `xunit.runner.visualstudio` 3.1.5 to the stack table alongside `Microsoft.NET.Test.Sdk` 18.8.1.
>
> **Option B (MTP, which is where xunit.v3 is heading):** drop `Microsoft.NET.Test.Sdk` and add to `global.json`:
> ```json
> {
>   "test": {
>     "runner": "Microsoft.Testing.Platform"
>   }
> }
> ```
> Note that xUnit has announced MTP v1 support is removed from package version 4.0.0 onward, with MTP v2 becoming the default, so Option B is the direction of travel — but 4.0.0 is still prerelease (`4.0.0-pre.154`) and 3.2.2 is correctly chosen for now.
>
> Either way, the CI workflow must assert a **non-zero executed test count**, not merely a zero exit code. A test stage that silently discovers nothing is indistinguishable from a passing one.

### VR-4 — no fake clock for a design that mandates a deterministic one

AD-11 and the conventions table commit hard to injected time: "against the injected `TimeProvider` and never `DateTime.Now`, so every signal is deterministic under test," and "`DateTime.Now` and `DateTime.UtcNow` are banned in application and domain code." The Learn page for `TimeProvider` names the intended mechanism: "To make it easier to test time-dependent code, you can use `FakeTimeProvider` from the `Microsoft.Extensions.Time.Testing` package."

That type ships in the NuGet package **`Microsoft.Extensions.TimeProvider.Testing`**, current stable **10.8.0** (note the package id differs from the namespace; `Microsoft.Extensions.Time.Testing` is not a package id and returns `BlobNotFound` from the flat container). Without it, the expiry-sweep and staleness-signal tests will end up hand-rolling a stub `TimeProvider`, which is a small but avoidable divergence from the first-party pattern the conventions imply. Add the row.

## Part 4 — Claims that were checked and found sound

Recorded so a later reader does not re-litigate them:

- Every prerelease trap was avoided correctly. Where the newest artifact on a feed was a prerelease, the spine took the newest *stable*: Serilog.Sinks.File (7.0.0, not `8.0.0-nblumhardt-02322`), Shouldly (4.3.0, not `5.0.0-preview.2`), xunit.v3 (3.2.2, not `4.0.0-pre.154`), and all the `Microsoft.*` 10.0.10 packages (not `11.0.0-preview.6.26359.118`).
- `Serilog.AspNetCore` 10.0.0, `Scalar.AspNetCore` 2.16.16, and `Azure.Monitor.OpenTelemetry.AspNetCore` 1.6.0 each ship an explicit `net10.0` asset group per their nuspecs, so none of them will be consumed through a downlevel or `netstandard2.0` fallback asset.
- `react-dom@19.2.8` declares `react ^19.2.8`, so the single "React / React DOM | 19.2.8" row is not a simplification that hides a mismatch — the pair is exact by design.
- FluentValidation and its DI extensions genuinely share version 12.1.1, so combining them on one row is accurate rather than sloppy.
- SQLite's lack of `SKIP LOCKED` (the stated reason for deferring competing-consumer outbox draining) is real, not folklore.
- The header claim "Verified against the NuGet flat-container API and the npm registry on 2026-07-28" is substantially true. It is not a rubber stamp — the pins bear it out to a degree that is rare in a document of this kind.

## Verdict

**PASS WITH TWO REQUIRED CORRECTIONS.**

No architectural decision in this spine rests on a false technical premise. Every claim the mandate singled out as decision-bearing — FluentAssertions' commercial licence, SQLite partial unique indexes behind AD-3, `Guid.CreateVersion7()`, RFC 9457 obsoleting 7807, `TimeProvider` in `net10.0`, .NET 10's LTS status, the OpenAPI-plus-Scalar posture, and Azure Monitor OpenTelemetry over the classic Application Insights SDK — is independently confirmed against a primary source. The TypeScript 6-over-7 decision is not merely defensible, it is the only choice that resolves against `typescript-eslint` today, and the GA date cited in the Deferred table matches the registry timestamp exactly.

What must change before this table is treated as build substrate:

1. **VR-1 (Critical):** bump Node.js from 22.18.0 to at least 22.23.1 and npm from 10.9.3 to 10.9.8, because `react-router@8.3.0` declares `engines.node >=22.22.0`.
2. **VR-2 (Critical):** resolve the test-runner composition — add `xunit.runner.visualstudio` 3.1.5, or drop `Microsoft.NET.Test.Sdk` and set the Microsoft Testing Platform runner in `global.json` — and make CI assert a non-zero executed test count.

Then three small additions that prevent avoidable friction: name the router package as `react-router` (VR-3), add `dayjs` for `@mantine/dates`, and add `Microsoft.Extensions.TimeProvider.Testing` 10.8.0 for the determinism the conventions already require (VR-4). While implementing AD-3, use SQLite double-quoted identifiers in `HasFilter` rather than the SQL Server bracket syntax shown in the EF Core documentation, and make the persisted representation of `Status` explicit so the filter and the value converter cannot drift.
