# KAFF-105b · `GET /api/auth/me` returns the projects I reach, and how I reach them

**Slice:** 1 · **Epic:** Foundation · **Points:** 3 · **Status:** Ready. **Proposed for sprint 2 and not yet locked by Nabil.** All ten of its criteria are payload criteria; **none of them builds a screen**, so D-091's deferral of `AC-101b-A` (the staff shell) onto this story cannot be discharged by this story as written. Either it grows a frontend half and re-estimates — `process/agile.md` puts a story spanning backend and frontend at 8 — or the shell is a story the BA has not written yet. Not resolved here; see `meetings/2026-08-30-sprint-2-open.md``
**Spec:** §9, §12 · **Decisions:** D-010, D-035, D-044 (rulings 2, 3, 5), **D-051 (Q32)**
**Depends on:** KAFF-105a, KAFF-113, KAFF-114

> **Split from KAFF-105** on 2026-08-21 (D-051). The identity half is **KAFF-105a**. This half was
> blocked on **Q32** — what HR may see of a project — and **Q32 is now answered**.

## Story
As the frontend, I learn which projects the signed-in user reaches, at what seniority, and by what
route — so the shell can render a project switcher without re-implementing `PermissionCatalogue` in
TypeScript, and without ever showing a project to somebody who cannot open it.

## What Karim ruled, and the shape of the answer
> *"HR may only see the project name and the list of assigned engineers … If the main project
> dashboard contains financial data, HR must be routed to a separate 'Project Team' tab/screen that
> contains zero financial details."* — **D-051 (Q32)**

**Note the shape: a separate surface, not a filtered view.** D-051 records why, and it is the same
pattern and the same reason `spec.md` §12 uses for the client portal — *"a filtered view leaks the
first time somebody adds a field."* It also resolves the tension D-044 ruling 2 created and answers a
question that had been asked three times in three registers (Q-UX-3, QA-2, findings F-03 and F-13).

**This does not grant HR `ProjectRead`**, and granting it would undo D-044 ruling 2, which makes HR
*"strictly administrative"* with *"zero financial visibility."* D-051 says the ruling *"implies a new
narrow permission"* and leaves the naming to this story.

### The new permission: `ProjectTeamRead`
| | |
|---|---|
| **Name** | `ProjectTeamRead` |
| **Grants** | `Role.Owner`, `Role.Hr` |
| **Scope** | `ProjectScoped`, with the same global reach `ProjectAssignmentManage` already has for both roles (D-044 rulings 3, 4) — HR staffs projects it was never assigned to, so it must be able to name them |
| **Carries** | a project's name, its code, and the list of assigned people with their roles and levels. **Nothing else, ever** |
| **Does not carry** | any value, cost, margin, balance, budget, status, date or client identity |

It is one new row in `PermissionCatalogue` mirroring an existing row's grant and reach, not a new
permission model. The surface it opens is KAFF-115's team panel on its own route (see that story's
rule 5).

**It does not exist yet, and this story is where it is born.** `Permission` has no `ProjectTeamRead`
member and `PermissionCatalogue` has no such row
[Verified: 2026-08-22 @ `src/Domain/Authorization/Permission.cs`, @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Build` — neither names it]. **No slice-1 test may assert it before the row lands.**

**SM-30 binds this row** (`process/agile.md`): the row and a test naming it land in the **same**
change, and the test name the row's comment cites must be one that exists. This is not hypothetical
here — the `ProjectManage` row cited `Opening_a_project_needs_no_project`, a name that existed only
as a proposal and never in `tests/`. **That citation has since been repointed** and the row now names
`An_unassigned_holder_of_ProjectManage_cannot_edit_a_project` and
`Only_the_owner_and_the_technical_office_may_open_a_project`, both of which exist
[Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.ProjectManage`;
@ `tests/Domain.Tests/PermissionEvaluatorTests.cs` -> `An_unassigned_holder_of_ProjectManage_cannot_edit_a_project`].
Three rows shipped on 2026-08-22 named in no test at all (D-056 §3); do not make `ProjectTeamRead`
the fourth.

**And the permission is not the whole control — the projection is.** The same warning D-055 §2 records
for `UserRead` applies exactly: a response type that satisfies `ProjectTeamRead` and carries a
financial field breaks rule 8 while passing every permission test. That is what AC-105b-F is for.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | The response lists every **active** `ProjectAssignment` for the caller, each with its `AssignmentLevel` | §9 (*"Permission = role × assignment"*) · D-044 ruling 5 |
| 2 | Project-scoped permissions are returned **per project the user reaches**, computed from `PermissionCatalogue` — never from a hand-written list | §9 · D-012 · `PermissionScope` |
| 3 | Each project says **how** the caller reaches it. *"Owner, globally"* and *"assigned on 3 June"* are different facts and the UI must not merge them. **Do not invent a vocabulary for this — the domain already has one.** `ProjectAccessPath` is the enum, and it carries **four** grant values, not three: `OwnerGlobal`, `HrGlobal`, `Assignment` and `PortalClient`, plus `None`, which is the only value a refusal may carry [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionEvaluator.cs` -> `enum ProjectAccessPath`]. `IProjectAccessPolicy` returns it on `ProjectAccess`, and `ProjectAccess.Granted` is **derived** from `Path` rather than stored beside it [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionEvaluator.cs` -> `record ProjectAccess`; @ `src/Infrastructure/Authorization/ProjectAccessPolicy.cs` -> `EvaluateAsync`] | D-010 · D-044 ruling 3 · D-055 §7 · KAFF-116 (the same distinction the audit trail records) |
| 4 | Revoked assignments are not listed — the payload says who can act now, and history is the audit trail's job. `ProjectAssignment.IsActive` is a computed `RevokedAt is null`, not a stored column [Verified: 2026-08-22 @ `src/Domain/Identity/ProjectAssignment.cs` -> `IsActive`, `RevokedAt`] | slice 0 `ProjectAssignment.IsActive` |
| 5 | The Owner reaches every project with no assignment row, so the Owner's project list is *"every project that exists"*. The policy grants the Owner `OwnerGlobal` at `AssignmentLevel.Supervisor` and HR `HrGlobal` at `Standard`, and the asymmetry is deliberate [Verified: 2026-08-22 @ `src/Infrastructure/Authorization/ProjectAccessPolicy.cs` -> `EvaluateAsync`, `GlobalReachAsync`] | D-010 · D-044 ruling 3 |
| 6 | **An `Role.Hr` caller receives every project that exists, with its name and code and nothing else** — and every entry is flagged as reachable only through the Project Team surface, never the project dashboard | **D-051 (Q32)** · D-044 rulings 3, 4 |
| 7 | **HR receives every project, including ones with nobody on them.** D-044 ruling 4 is explicit that requiring an assignment in order to create assignments is circular: *"on a new project nobody is assigned, so nobody could make the first one."* A list of only-staffed projects would make the first assignment on a new project impossible | D-044 ruling 4 |
| 8 | **HR's entries carry no financial field, and no field that could become one.** Not value, not budget, not balance, not margin — and the response type HR receives is not the response type staff receive | **D-051 (Q32)** · D-044 ruling 2 · §12 (the portal precedent) |
| 9 | HR is not granted `ProjectRead` and does not receive the project dashboard's payload under any circumstance | **D-051 (Q32)** · D-044 ruling 2 |
| 10 | A `Role.Client` receives no project belonging to any other client. The portal is a separate host and a separate surface (D-051 Q33, D-035), so a client is not expected here at all — **this rule is the guard for if one arrives, not a feature** | §12 · D-035 · **D-051 (Q33)** |
| 11 | A `Role.SiteEngineer` may be Supervisor on one project and Junior on another; the level is per row and is never flattened to one value for the user | D-044 ruling 5 |
| 12 | What this endpoint returns **decides nothing**. Every request against a project is authorised again server-side | §9 · CLAUDE.md |

## Permissions, money, audit, i18n
- **Permissions:** authenticated, any role, no assignment. It returns only the caller's own reach.
  The **new** `ProjectTeamRead` is what HR's entries are computed against.
- **Money:** moves no money and **carries no money field for anybody** — not only for HR. This is the
  endpoint the whole shell calls on every navigation; a "project value" added here would reach the
  portal (§12) and HR (D-044 ruling 2) in the same commit.
- **Audit:** none. It is a read.
- **i18n:** none in the payload — identifiers only, resolved client-side. The Project Team screen's
  own strings belong to KAFF-115. **The server never sends prose** (`problem-details.ts`, slice 0).

## Acceptance criteria
**AC-105b-A — an engineer sees his own seniority, per project**
Given I am a Site Engineer, Supervisor on project A and Junior on project B
When I call `GET /api/auth/me`
Then both assignments are listed with their own levels
And project A carries `DraftSubmit` while project B does not

**AC-105b-B — the Owner's reach needs no assignment row**
Given I am the Owner with no `ProjectAssignment` rows at all
When I call `GET /api/auth/me`
Then every project that exists is listed, and each is marked as reached by Owner-global rather than by assignment

**AC-105b-C — HR gets names, and only names** *(fails if the rule is broken)*
Given I am `Role.Hr`, and three projects exist with budgets, balances and contract values set
When I call `GET /api/auth/me`
Then all three are listed with name and code
And the payload is inspected field by field and contains **no** value, cost, margin, balance, budget, status or client field
And `ProjectRead` appears nowhere in my permissions

**AC-105b-D — HR sees a project nobody is on yet** *(fails if the rule is broken)*
Given a project created this morning with zero assignments
When an HR user calls this endpoint
Then that project is listed — otherwise nobody could ever make its first assignment (D-044 ruling 4)

**AC-105b-E — HR is routed to the team surface, not the dashboard** *(fails if the rule is broken)*
Given the HR payload from AC-105b-C
When each project entry is read
Then each is flagged as reachable through the Project Team surface only
And an HR call to the project dashboard endpoint is refused with 403

**AC-105b-F — the surfaces are separate types, not one type filtered** *(fails if the rule is broken)*
Given the response type HR receives and the response type a Technical Office user receives
When the code is read
Then they are different types, and a field added to the staff type cannot appear in HR's without somebody adding it there deliberately

**AC-105b-G — a portal client is bounded** *(fails if the rule is broken)*
Given I am `Role.Client` for client X, which has one project, and client Y has another
When I call this endpoint
Then only X's project is named, and Y's project appears nowhere in the payload

**AC-105b-H — a revoked assignment disappears**
Given my assignment to project A was revoked this morning
When I call `GET /api/auth/me`
Then project A is not listed

**AC-105b-I — a role change empties the list on the next call** *(fails if the rule is broken)*
Given a Site Engineer on three projects whose role the Owner changes to Technical Office (KAFF-109, D-051 Q27)
When they call this endpoint again with the same session
Then the project list is empty

**AC-105b-J — the catalogue drives the per-project permissions** *(fails if the rule is broken)*
Given a `ProjectScoped` permission is added to `PermissionCatalogue` with a grant for Technical Office
When a Technical Office user assigned to project A calls this endpoint
Then it appears under project A with no change to this endpoint's code

## Not in this story
The identity half (KAFF-105a). The Project Team screen itself and its panel — **KAFF-115**, which
gains the `ProjectTeamRead` route. Creating or revoking assignments (KAFF-113, KAFF-114). The
navigation shell's menu, which is convenience, not security.

## Questions for Karim
None. **Q32 is closed by D-051.**
