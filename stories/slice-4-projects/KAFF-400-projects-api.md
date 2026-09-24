# KAFF-400 · The Projects API — create, list, read

<!-- kaff id=KAFF-400 slice=4 points=8 state=READY -->

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
