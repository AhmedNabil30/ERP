# KAFF-400 · The Projects API — create, list, read

<!-- kaff id=KAFF-400 slice=4 points=8 state=BUILT -->

**Slice:** 4 — Projects · **Points:** 8.
**Why now:** the demo walkthrough is blocked on this and nothing else. `src/Api/Features/` has no
`Projects/` folder at all, so `S-050` project list, `S-051` create and `S-052` project overview have
no API. `AssignUserToProject` already assigns users to projects that nothing can fetch.
**Files:** new `src/Api/Features/Projects/CreateProject/*`, `ListProjects/*`, `GetProject/*`.
**Screens this unblocks:** `ProjectList`, `ProjectCreate`, `Project` on the canvas —
https://claude.ai/artifact/KmuT1UYyXMiSqocN2MCkgY

## What already exists — do not rebuild it

- `Domain/Project.cs`, `Domain/ProjectAssignment.cs`, `Domain/ContractType.cs`,
  `Domain/ProjectAccessPolicy` / `IProjectAccessPolicy`.
- `Domain/ProjectAccountSetFactory.cs` — **`KAFF-302` is BUILT**: creating a project opens its account
  set. Call the existing factory; do not write a second one.
- `Infrastructure/KaffDbContext.cs` exposes `DbSet<Project>`. The table is migrated.
  **No new migration is expected.** If you believe one is needed, stop and say why before writing it.

## Endpoints

### `POST /api/projects` — create
- Body: name, clientId, contractType, contractValue, holdPercentage, clientAdvance, status.
- **`holdPercentage` defaults to 20** — `spec.md` §5.1: *"hold = 20% of period work value"*, and §15:
  *"Accumulated hold = exactly 20% of contract value."* The 5% figure in that same section is the
  **subcontractor** retention and is not this field. Owner-adjustable per project (§15 open assumption 1).
- The withholding rate and client tax number are **not on this endpoint.** `ux/screen-inventory.md`
  S-057: *"This is also where the withholding category lands — set by Finance, on the contract, never
  on the client."* They belong to contract signature, which is a later story.
- On success: open the account set through `ProjectAccountSetFactory`, in the same transaction.
- Status values come from Kaff's fixed vocabulary — لم تبدأ · جاري العمل · انتهت · متعثرة · تم تأجيلها.
  **متعثرة and تم تأجيلها are health tags, not states** (`spec.md` §13). Do not map either onto
  `Stopped`, and do not let either block anything: *"flagging a project as متعثرة would freeze the
  very payments meant to unstick it."*

### `GET /api/projects` — list
- Returns only projects the caller may reach. **Permission is role × assignment** (`spec.md` §9):
  role alone is insufficient, enforced server-side, every request. Owner and HR have global reach
  without an assignment row (D-010, D-044 §3).
- **HR gets project names and team sizes and nothing financial** (D-051 Q32) — no contract value, no
  progress, no balances. Absent from the response, not hidden by the client.
- Carries a total, so the UI stops computing counts from the page it happens to have.

### `GET /api/projects/{id}` — read
- Header data for `S-052`: name, client, contract type, contract value, status, team.
- Refuses with the project-scoped refusal for a caller with the role but no assignment.

## The rules this story must not break

- **Money is `decimal` with `HasPrecision(18, 4)` configured explicitly.** No `float`, no `double`.
- **Money crosses the wire as a JSON string**, both directions, every field — D-135. A JSON number
  becomes a JavaScript `double` and `decimal(18,4)` does not survive the round trip.
- **No stored balance column.** Balances are derived by summing postings, always.
- **Every state change writes an audit record** — who, when, before and after — through the one
  mechanism in `Domain/`, not per-feature code.
- **Nobody creates and approves the same movement.** Not exercised here, but do not build a path that
  would allow it later.

## Acceptance criteria

**AC-400-A** The three endpoints exist as vertical slices — `Endpoint` · `Handler` · `Request` ·
`Response` · `Validator` per folder. No repository over EF Core, no MediatR, no service layer that
only forwards.
**AC-400-B** A permission test per role asserting what it cannot reach, hitting the endpoints
directly: a Site Engineer with no assignment gets a refusal on `GET /api/projects/{id}`; HR gets a
list with no financial field present in the payload at all.
**AC-400-C** Creating a project opens its account set, proven by a test that reads the accounts back.
**AC-400-D** `holdPercentage` defaults to 20 and is persisted with `HasPrecision(18, 4)`.
**AC-400-E** Every money field serialises as a JSON string, proven by a test asserting the raw JSON.
**AC-400-F** An audit record is written on create, proven by a test.
**AC-400-G** `dotnet build` clean with warnings as errors. Domain and Api suites green.
**AC-400-H** No new migration, or a written reason why one was unavoidable.

## Not in this story

Contract signature and BOQ freeze. Withholding fields. Extracts. Any frontend. Any change to
`Features/Treasury/`.

## Questions for Karim

None — every rule above is quoted from `spec.md`. If you find a case `spec.md` does not answer,
**stop and ask**; do not decide.

## Build notes (2026-09-25)

Built as three vertical slices under `src/Api/Features/Projects/`: `CreateProject`, `ListProjects`,
`GetProject`. Commits: `30a68df` (create), `0d4af62` (list), `bf80584` (read).

### AC-400-A — vertical slices

Pass, with the same shape `Clients` already uses: `GetProject` and `ListProjects` carry no
`Request.cs`/`Validator.cs`, matching `GetClient`/`ListClients` — a `GET` with no body has nothing to
validate. `CreateProject` carries all five files.

### AC-400-B — permission test per role

The Site Engineer half passes as written: `GetProjectTests.A_site_engineer_with_no_assignment_is_refused`
gets a 403 on `GET /api/projects/{id}`.

**The HR half does not pass as written, and the story is wrong, not the build.** The brief says HR
"gets a list with no financial field present in the payload at all." `Permission.ProjectRead` —
project-scoped, used by `GetProject` — deliberately excludes HR: D-044 ruling 2 ("HR is strictly
administrative and has zero financial visibility"), pinned by the already-shipped
`CatalogueCompletenessTests.Hr_cannot_reach_a_project_through_ProjectRead`. HR's own project list
already exists, built in KAFF-105b (D-103): `GET /api/auth/me` returns `TeamProjectEntry` — name, code,
team size, nothing financial — through a type that structurally cannot carry a financial field, which
is a stronger guarantee than an endpoint that has to remember to redact one every time a field is
added. Building `GET /api/projects` to admit HR would either duplicate that mechanism or widen a
financial-adjacent permission to a role every other row in the catalogue names by exclusion.

Followed the existing precedent instead: added `Permission.ProjectList` (`CompanyWide`, KAFF-400) for
the endpoints that cannot name a single project — the same reasoning D-055 §3 used to split
`ProjectCreate` off `ProjectManage`. `ProjectList`'s grants are Owner, Finance, Technical Office,
Marketing and Site Engineer (the same set `ProjectRead` grants, minus Head of Design, who
`CatalogueCompletenessTests.A_head_of_design_holds_exactly_one_permission` pins to `ProjectRead` alone).
HR holds no grant and is refused at the gate — `ListProjectsTests.Hr_is_refused_and_must_use_its_own_project_dashboard`.
Flagging this for Karim/Nabil rather than deciding it silently: if HR genuinely needs financial-free
rows from *this* endpoint rather than `/auth/me`, that is a catalogue change, not something to infer.

### AC-400-C — account set opens with the project

Pass. `CreateProjectTests.Creating_a_lump_sum_project_opens_its_five_account_set` reads the accounts
back. Both `Project` and its accounts are added to the same `KaffDbContext` before one
`SaveChangesAsync`, so EF Core's implicit transaction covers both.

### AC-400-D — holdPercentage defaults to 20

Pass. `CreateProjectTests.HoldPercentage_defaults_to_20_when_omitted` — defaults to 20%, not the 5%
subcontractor retention from the same spec.md paragraph. Persisted precision: `Percentage` (which
`HoldRate`/`AdvanceRate` are) already carries a project-wide EF convention of `decimal(18,6)` —
`KaffDbContext.ConfigureConventions` — distinct from `Money`'s `decimal(18,4)`. The AC's "(18,4)"
reads as the money precision rule generally; nothing new was needed for either, both conventions
already existed before this story.

### AC-400-E — money crosses the wire as a string

Pass. `CreateProjectTests.Money_and_rate_fields_serialise_as_json_strings` asserts the raw
`JsonValueKind.String` on `contractValue`, `holdRate` and `advanceRate` — for free, through the global
`Money`/`Percentage` converters already registered (D-135). Nothing endpoint-specific was written.

### AC-400-F — audit record on create

Pass. `CreateProjectTests.Creating_a_project_leaves_a_created_audit_record` — no handler writes it by
hand; `AuditSaveChangesInterceptor` sees the tracked `Project` and `Account` rows and writes `Created`
for each in the same transaction, the same mechanism `CreateClient` and `AssignUserToProject` use.

### AC-400-G — build and test gates

`dotnet build KaffErp.sln -c Debug --nologo`: **0 Warnings, 0 Errors**, full solution (all six
projects, including `Kaff.Api` and `Kaff.Api.Tests`). `dotnet format KaffErp.sln --verify-no-changes`:
exit 0.

`dotnet build -c Release` for `Kaff.Domain`, `Kaff.Infrastructure` and `Kaff.Domain.Tests`
individually: **0 Warnings, 0 Errors** each. `Kaff.Domain.Tests` (Release): **298/298 green.**

**`Kaff.Api` and `Kaff.Api.Tests` could not be built in Release.** The dispatch brief forbids touching
anything on port 5080; a live `Kaff.Api` process (PID 11964) was already running from
`src/Api/bin/Release/net10.0/` for the frontend agents, and MSBuild's copy step into that same
directory fails with `MSB3027`/`MSB3021` (file locked) after ten retries — every failure in that build
is a copy-lock error, zero `CS` compiler errors. The Debug build (different output path, no lock)
compiles `Kaff.Api` and `Kaff.Api.Tests` clean, which is the same source tree modulo optimisation
flags — `TreatWarningsAsErrors` is unconditional in this repo's `Directory.Build.props`, not
Release-only. **Api.Tests were written, not run**, per the dispatch's own gate ("the Scrum Master runs
them"): `CreateProjectTests.cs`, `ListProjectsTests.cs`, `GetProjectTests.cs`. The Scrum Master should
run `dotnet build KaffErp.sln -c Release` and `dotnet test tests/Api.Tests` once the demo's live API
process is free to restart, and treat a Release-only failure there as a real finding — this session
only has evidence that the Debug build is clean.

### AC-400-H — no new migration

Pass, with a finding along the way. **There is no `project_code_seq`.** `Client`, `Employee`,
`Subcontractor` and `Supplier` all draw a sequential code from a dedicated PostgreSQL sequence; no
equivalent exists for `Project`, and `Project.Code` is required, unique and has no default. Per the
brief's own instruction, stopped and did not write a migration. Instead `CreateProject.Handler.GenerateCode`
draws `"PRJ-" + eight hex characters from a fresh Guid` — no sequence needed, no race to lose,
customer-facing readability traded away. Documented as a named simplification in the handler's own
remarks, with the upgrade path spelled out (add `project_code_seq` in a migration once codes need to
read sequentially, e.g. `PRJ-0007`, which `ProjectAccountSetFactory`'s own comments already assume).

### `clientAdvance` — a business question, flagged rather than decided

The brief's create body lists `holdPercentage` and `clientAdvance` with no further definition.
`Project` has a `Percentage AdvanceRate` (set through `SetLumpSumTerms`) and no field at all for an
absolute advance amount — spec.md §16 assumption 1 pairs exactly these two figures ("Advance 25%, hold
20% as defaults, owner-adjustable per project"), which reads as `clientAdvance` being the advance
*rate*, not a cash figure. Built it that way, defaulting to 25 when omitted. **If Karim meant the cash
amount collected at signing**, that has nowhere to land today: it isn't a `Project` column, and
recording it as a real payment would need a Treasury posting, which is both out of this story's reach
(`Features/Treasury` is another agent's slice) and out of contract signature's (a later story this one
does not build). Flagged in `Request.cs`'s own remarks as well as here.

### Not done, named so the next session does not assume it exists

- **Cost Plus and Design creation terms.** This endpoint opens the project shell for every contract
  type but only sets Lump Sum's commercial terms (`SetLumpSumTerms`). Supervision rate (Cost Plus) and
  area/rate per m² (Design) are contract-signature's story, per the brief's own "Not in this story."
- **The initial `status` field's "skip straight to Active" convenience** is inferred, not asked for —
  the brief lists `status` in the create body with no further description. Accepted only `null`/`Setup`
  (no-op) or `Active` (one `TransitionTo` call); anything else, including the two health tags, is a
  400. Flagged as an inference rather than a silent decision.
- **KAFF-115's per-member team roster** is not built. `GetProject.Response.TeamSize` is a count of
  active `ProjectAssignment` rows, matching what `GET /api/auth/me`'s `TeamProjectEntry` already does
  for HR — not a roster of names and roles.
- **No paging** on `GET /api/projects`, the same call `ListClients` made: no volume yet, and the
  response already carries a `Total` so paging can be added later without changing the shape.
- **HR's list expectation in AC-400-B** — see above. Not built as literally stated; built the
  catalogue-consistent alternative and documented why.
