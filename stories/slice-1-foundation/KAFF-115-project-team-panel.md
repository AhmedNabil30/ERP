# KAFF-115 · The project team panel is built from assignment rows, not from the access check

**Slice:** 1 · **Epic:** Foundation · **Points:** 8 · **Status:** BLOCKED — transitively on KAFF-105b (BLOCKED on `Q43`), and on its own account. **Re-estimated 3 → 8, 2026-09-01** (`meetings/2026-09-01-sprint-2-refinement.md` §3.3): it births the `ProjectTeamRead` permission row (`process/agile.md` puts touching the permission model at 5) and spans backend and frontend through `AC-115-J` (8) — take the higher, not the sum. Frontend, asked independently at refinement, returned 8 with the same reasoning. Depends on KAFF-105b, which defines the payload this panel reads. `AC-101b-D` lands with it — **and does not discharge it.** ⚠️ **`AC-101b-D` fails the same arithmetic `AC-101b-A` does, found 2026-09-01, not before.** HR lands on **S-009a**, the project *list* (`ux/navigation.md` -> `Landing summary`); this story builds **S-009b**, one project's *team panel* — `AC-115-H` opens *"the Project Team screen **for project A**"*, which is per-project, not the list `AC-101b-D` requires HR to land on. **Neither story discharges `AC-101b-D` as written, and no reading is picked here** — the same note stands in `stories/slice-1-foundation/KAFF-101b-sign-in-screen.md`, and `meetings/2026-09-01-sprint-2-refinement.md` §3.1 explains why all three previously-costed readings of the shell question understate the hole. This story's own three other Definition of Ready failures — `AC-115-H` and `AC-115-G`'s money/dashboard-endpoint and wrong-reason defects, and `AC-115-I`'s unfailable "when the code is read" shape — are named in that same meeting §3.3 and are **not repaired in this revision**; carried forward so nobody assumes they are fixed
**Spec:** §9, §12 · **Decisions:** D-010, D-044 (rulings 2, 3), **D-051 (Q32)**
**Depends on:** KAFF-113, KAFF-114, KAFF-105b *(which names the permission)*

> **Re-estimated 2 → 3 on 2026-08-21.** Q32 is answered and the answer adds a second surface and a
> new permission to this story — HR's Project Team screen (D-051 Q32). It is the same data behind a
> different route and a different permission, not a second implementation.

## Story
As anyone who can open a project, I see who is on its team — and the list is the assignment rows, not
"everybody the access check would let in", because the second list contains Karim on every project in
the company.

Raised at the kickoff, BA → UX: *"build the project team panel from `ProjectAssignment` rows, never
from the access check, or Karim appears on every project team in the system"*
(`meetings/2026-08-18-slice-1-kickoff.md` §4). Since then the same became true of HR (D-044 ruling
3), so there are now two actors who reach every project without being on any team.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | The team is the set of **active** `ProjectAssignment` rows for the project | §9 |
| 2 | The Owner and HR reach every project without an assignment row and therefore appear on no team panel unless somebody actually assigned them. Both go through the same row-less branch, distinguished only by `ProjectAccessPath.OwnerGlobal` / `HrGlobal` [Verified: 2026-08-22 @ `src/Infrastructure/Authorization/ProjectAccessPolicy.cs` -> `EvaluateAsync`, `GlobalReachAsync`] — **so the access check can never be the source of this panel** | D-010 · D-044 ruling 3 |
| 3 | Each member shows name, role and — for site engineers — `AssignmentLevel`, because seniority is per project [Verified: 2026-08-22 @ `src/Domain/Identity/ProjectAssignment.cs` -> `Level` — the level is on the assignment row, not on the user] | §9 · D-044 ruling 5 |
| 4 | Revoked members are not in the panel. History belongs to the audit trail. `IsActive` is the computed `RevokedAt is null` [Verified: 2026-08-22 @ `src/Domain/Identity/ProjectAssignment.cs` -> `IsActive`, `RevokedAt`] | slice 0 `ProjectAssignment.IsActive` |
| 5 | **The team is reachable from two surfaces, and they are separate surfaces, not one view filtered.** Staff read it inside the project with `ProjectRead`. **HR reads it on its own "Project Team" screen with the new narrow `ProjectTeamRead`** — Karim: *"HR may only see the project name and the list of assigned engineers … HR must be routed to a separate 'Project Team' tab/screen that contains zero financial details."* This closes **Q32** and finding **F-03** | **D-051 (Q32)** · KAFF-105b (which defines `ProjectTeamRead`) |
| 5a | **HR is not granted `ProjectRead`**, and a route that would give HR the project dashboard is a defect. Granting it would undo D-044 ruling 2, which makes HR *"strictly administrative"* with *"zero financial visibility"* | **D-051 (Q32)** · D-044 ruling 2 |
| 5b | The separate surface is chosen for the reason `spec.md` §12 uses for the client portal: **a filtered view leaks the first time somebody adds a field.** Both surfaces read the same assignment rows; neither shares a response type with the project dashboard | **D-051 (Q32)** · §12 · D-035 |
| 6 | A portal client never sees it. §12 lists what the client sees, and the team is not on the list | §12 |
| 7 | A deactivated member is absent from the panel because deactivation revokes their assignments (D-049 ruling 5), not because the panel filters on the user's `IsActive`. One mechanism, and it lives in KAFF-111 | D-049 ruling 5 · KAFF-111 |

## Permissions, money, audit, i18n
- **Permissions:** `ProjectRead`, `ProjectScoped`, for the in-project panel — assignment required for
  everyone except the Owner, whose global reach is a reach rule (D-010). **Or `ProjectTeamRead`,
  `ProjectScoped`, granted to `Role.Owner` and `Role.Hr` with the same global reach
  `ProjectAssignmentManage` already gives both** (D-044 rulings 3, 4), for the Project Team screen.
  One new catalogue row, mirroring an existing row's grant and reach — defined in KAFF-105b.
  **It does not exist yet** [Verified: 2026-08-22 @ `src/Domain/Authorization/Permission.cs` and
  `src/Domain/Authorization/PermissionCatalogue.cs` — no `ProjectTeamRead` member and no such row],
  and **SM-30 binds it**: the row and a test naming it land in the same change, and the test name the
  row's comment cites must be one that exists. Three rows shipped named in no test on 2026-08-22
  (D-056 §3); this must not be the fourth.
- **Money:** moves no money and shows no money — no rate, no cost, no salary. A team panel showing
  what an engineer costs would put payroll in front of everyone who can open a project.
- **Audit:** none. It is a read.
- **i18n:** `team.title`, `team.empty`, `team.member.role`, `team.member.level`, and
  `team.screen.title` for HR's separate surface, reusing `enum.Role.*` and `enum.AssignmentLevel.*`. *(Was `users.role.*` / `assignments.level.*` until 2026-08-22 — `enum.<Type>.<Member>` is the shape for a server enum rendered as text [Verified: 2026-08-22 @ `ux/rtl-and-i18n.md` -> `enum.<Type>.<Member>`]. Finding **V-07** under **SM-15**. This story is deferred out of sprint 1; the pass covers the whole slice-1 key list so the divergence does not survive into the sprint that builds it.)*

## Acceptance criteria
**AC-115-A — the Owner is not on every team** *(fails if the rule is broken)*
Given an Owner who has never been assigned to project A, and one Technical Office user who has
When project A's team panel is read
Then it contains exactly one member, and the Owner is not in it

**AC-115-B — nor is HR** *(fails if the rule is broken)*
Given an HR user who assigned everyone on project A and holds no assignment row
When the panel is read
Then HR is not in it

**AC-115-C — seniority shows, per project**
Given a Site Engineer who is Supervisor on A and Junior on B
When A's panel and B's panel are read
Then each shows the level for that project

**AC-115-D — revoked members are gone**
Given a member whose assignment was revoked
When the panel is read
Then they are absent

**AC-115-E — and a leaver is gone the same way** *(fails if the rule is broken)*
Given a team of two, one of whom the Owner then deactivates
When the panel is read, and then the audit trail for that project
Then the panel shows one member, and the trail still shows both and when the second came off (D-049 ruling 5)

**AC-115-F — an empty team has an explicit empty state**
Given a project with no assignments
When the panel is read
Then it renders `team.empty` — never a blank area and never a phantom row

**AC-115-G — a client cannot read it** *(fails if the rule is broken)*
Given I am a `Role.Client` user whose client owns project A
When I request project A's team panel
Then I am refused with 403 — `PortalRead` is not `ProjectRead`

**AC-115-H — HR reads the team, and reaches nothing else** *(fails if the rule is broken)*
Given I am `Role.Hr` with no assignment row, and project A has a budget, a contract value and a balance
When I open the Project Team screen for project A
Then I see its name, its code and its members with their roles and levels
And the payload contains no value, cost, margin, balance, budget, status or client field
And a request to the project dashboard endpoint for project A is refused with 403

**AC-115-I — the two surfaces are different types** *(fails if the rule is broken)*
Given the response type the Project Team screen returns and the project dashboard's type
When the code is read
Then they are different types, and a money field added to the dashboard cannot appear on the team screen without somebody adding it there

**AC-115-J — Arabic, RTL, at mobile width**
Given the panel at 390px in Arabic
When it renders
Then direction is RTL, names and Latin codes are bidi-isolated, and there is no horizontal overflow

## Not in this story
Adding or removing members (KAFF-113, KAFF-114). Showing who assigned whom and when — that is the
audit trail, KAFF-117, which is now `Ready`: Karim ruled the trail is the Owner's alone (D-049
ruling 1), so **nobody reading this panel can also read the history behind it unless they are the
Owner.** Stated here because it is a surprising consequence, not a defect.

## Questions for Karim
None. **Q32 is closed by D-051**, and it is what rules 5, 5a and 5b are built on.
