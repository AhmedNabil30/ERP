# KAFF-113 · Assign a user to a project, with seniority for site engineers

**Slice:** 1 · **Epic:** Foundation · **Points:** 5 · **Status:** Ready
**Spec:** §9, §8 · **Decisions:** D-010, D-012, D-044 (rulings 3, 5), D-035
**Depends on:** KAFF-106

## Story
As the Owner or HR, I put a person on a project, and choose whether a site engineer is Supervisor or
Junior on **this** project — because the same engineer can be a supervisor on one site and a junior
on another.

This is the second half of `spec.md` §9's *"Permission = role × assignment"*. Without a row here a
correctly-roled user is refused, which is the rule the whole permission model rests on.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | `ProjectAssignmentManage` is held by `Role.Owner` and `Role.Hr` | §9 · D-012 · D-044 rulings 1, 2 |
| 2 | HR assigns on **any** project that exists, with no assignment row of its own — *"HR does not need to be assigned to a project first in order to staff it"*. Requiring an assignment in order to create assignments is circular: on a new project nobody is assigned, so nobody could make the first one | D-044 ruling 3 |
| 3 | The Owner likewise reaches every project that exists, without an assignment row | D-010 · D-044 ruling 3 |
| 4 | HR's reach stops at a project that does not exist. The permission stays `ProjectScoped`, so the route must still name a real project — and the global-reach branch is itself bounded by the project existing [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.ProjectAssignmentManage`; @ `src/Infrastructure/Authorization/ProjectAccessPolicy.cs` -> `GlobalReachAsync`] | D-044 ruling 3 |
| 5 | Seniority lives on the assignment, not on the user — *"An engineer can be a Supervisor on one project and a Junior on another"* | D-044 ruling 5 |
| 6 | A level other than `Standard` is only legal for `Role.SiteEngineer`; `Standard` is only legal for everyone else. §9 attaches Junior/Supervisor to the site engineer role alone [Verified: 2026-08-22 @ `src/Domain/Identity/ProjectAssignment.cs` -> `Create`] | §9 · slice 0 `ProjectAssignment.Create` |
| 7 | `Role.Client` and `Role.Subcontractor` are never assigned. A portal user reaches a project through `Project.ClientId` matching `User.ClientId`, compared against the database and never against anything the request carried — and the path is named `PortalClient`, deliberately **not** folded into `Assignment`, because no assignment row exists [Verified: 2026-08-22 @ `src/Infrastructure/Authorization/ProjectAccessPolicy.cs` -> `ClientAccessAsync`; @ `src/Domain/Authorization/PermissionEvaluator.cs` -> `PortalClient`] | §9, §12 · D-035 |
| 8 | An inactive user is not assignable, and an assignment does not resurrect one | §9 · KAFF-110 |
| 9 | A user may hold only one active assignment per project. Re-assignment after revocation is legal — the unique index covers active rows only | slice 0 `ProjectAssignment` |
| 10 | The row records who assigned and when | slice 0 `ProjectAssignment` |

## Permissions, money, audit, i18n
- **Permissions:** `ProjectAssignmentManage`, `ProjectScoped`, Owner and HR — both with global reach
  per rule 2. **HR reaching a project does not mean HR can read it**: HR is deliberately absent from
  `ProjectRead` (D-044 ruling 2). **How HR picks the project is answered — Q32, D-051:** HR holds the
  new narrow `ProjectTeamRead` and works from a separate "Project Team" screen carrying a project's
  name and assigned people and **zero financial detail**. Whether the project's **code** may appear
  there is **Q43**, open. This endpoint is unchanged — it always took a project id — and findings F-03
  and F-13 are closed.
  **The surface arrives after this sprint.** `ProjectTeamRead` is defined in **KAFF-105b** and
  rendered by **KAFF-115**, and both are **deferred out of sprint 1**. Nothing in this story depends
  on either: the endpoint takes a project id from wherever the caller got it, and in this sprint the
  Owner gets it from the project list while HR gets it from a fixture. **The permission still does
  not exist** [Verified: 2026-08-22 @ `src/Domain/Authorization/Permission.cs` — the enum has no
  `ProjectTeamRead` member, and `PermissionCatalogue.cs` has no such row] — so no slice-1 test may
  assert it.

  **HR's picker for the *user* is answered — Q42 is CLOSED, D-055 §2.** HR holds **`UserRead`**,
  `CompanyWide`, granted to `Role.Hr` and `Role.Owner`
  [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.UserRead`; enum member at
  @ `src/Domain/Authorization/Permission.cs` -> `UserRead`], so HR can name a person to put on a project. **This
  endpoint was never affected either way.** *(This bullet said HR's half of the screen "is not
  buildable until Q42 is answered". It is answered.)*

  **Two things the closure does not do, and both must survive into whoever builds the read
  endpoint.** First: **the ruling is names and roles only** — no editing, and no visibility into
  salary if one is ever added. Second, and this is the one that gets lost: **the permission is not
  the whole control — the endpoint's projection is.** A `UserRead` endpoint returning the full user
  row satisfies the permission and **breaks the ruling**, because the row also carries usernames,
  departments and active state; `questions-for-karim.md` -> `Q42` warned in terms not to close Q42 *"by
  handing HR the Owner's user list"*. Nothing in the catalogue can stop that. **Name and role, and
  stop.** There is no such endpoint in slice 1.
- **Money:** moves no money. It decides who can later prepare, gate, approve and disburse on this
  project, which is why it is a 5 rather than a 2.
- **Audit:** `Created` on `ProjectAssignment` with `ProjectId` set, so the trail filters per project,
  actor = Owner or HR. KAFF-116 adds *how* the actor reached the project.
- **i18n:** `assignments.action.assign`, `assignments.assign.title`, `assignments.field.user`,
  `assignments.field.level`, `assignments.hint.level_per_project`,
  `enum.AssignmentLevel.Standard` / `.Junior` / `.Supervisor`, and the existing
  `errors.identity.assignment_level_not_applicable`, `errors.identity.client_is_not_assignable`,
  `errors.auth.not_assigned_to_project`.

  *(Corrected 2026-08-22 under **SM-15**, finding **V-07** / **N-05**. This bullet said
  `assignments.add` and `assignments.level.standard` / `.junior` / `.supervisor`. S-010 draws the
  button as `assignments.action.assign`
  [Verified: 2026-08-22 @ `ux/slice-1-flows.md` -> `assignments.action.assign`], and a server enum
  rendered as text is `enum.<Type>.<Member>`
  [Verified: 2026-08-22 @ `ux/rtl-and-i18n.md` -> `enum.AssignmentLevel.Supervisor`]. Neither spelling
  is in either catalogue yet.)*

## Acceptance criteria
**AC-113-A — HR staffs a project it was never assigned to** *(fails if the rule is broken)*
Given I am `Role.Hr` with no assignment rows anywhere
When I assign a Technical Office user to a project
Then the assignment is created

**AC-113-B — and still cannot open that project** *(fails if the rule is broken)*
Given I am the same HR user, one line later
When I call an endpoint requiring `ProjectRead` on that project
Then I am refused with 403

**AC-113-C — HR's reach stops at a project that does not exist**
Given I am `Role.Hr`
When I assign a user to a project id that does not exist
Then the request is refused, and the refusal is not a 500

**AC-113-D — the same engineer, two seniorities**
Given a Site Engineer
When HR assigns them Supervisor on project A and Junior on project B
Then both rows exist with their own levels, and each row's `AssignmentLevel` is the one it was given
And the permission evaluator grants `DraftSubmit` on A and refuses it on B, asserted against
`PermissionEvaluator` directly

*The second half read **"and `/api/me` reports `DraftSubmit` on A and not on B"** and has been
restated. Two defects in one line: the route is **`/api/auth/me`**, not `/api/me` (KAFF-105a fixes it
and says a mismatch here shows up as a 404 in a browser rather than as a failing test); and the
**per-project** permission list is **KAFF-105b**, which is deferred out of this sprint — KAFF-105a
returns company-wide permissions only. The evaluator is what actually decides, it exists in slice 0,
and asserting against it is both executable this sprint and closer to the rule. The endpoint-level
assertion belongs to KAFF-105b and is stated there.*

**AC-113-E — seniority is refused where §9 does not put it** *(fails if the rule is broken)*
Given a Finance user
When HR assigns them with level Supervisor
Then it is refused with `errors.identity.assignment_level_not_applicable`
And when a Site Engineer is assigned with level `Standard`, that is refused too

**AC-113-F — clients and subcontractors are not assignable** *(fails if the rule is broken)*
Given a `Role.Client` user and a `Role.Subcontractor` user
When HR attempts to assign either to a project
Then both are refused with `errors.identity.client_is_not_assignable`

**AC-113-G — nobody else can staff a project** *(fails if the rule is broken)*
Given I am Finance, then Technical Office, then a Supervisor Site Engineer assigned to the project
When each attempts to assign a user to it
Then every one is refused with 403 — being on the project is not permission to staff it

**AC-113-H — an inactive user is not assignable**
Given a deactivated user
When HR attempts to assign them
Then it is refused

**AC-113-I — no duplicate active assignment**
Given a user already assigned to project A
When HR assigns them to project A again
Then it is refused; and after the first is revoked, a new assignment succeeds

## Not in this story
Revoking (KAFF-114). Displaying the team (KAFF-115, **deferred**). Creating a project — nothing in
slice 1 creates one.

**Q17 is now closed in full, and the permission that opens a project is not the one this paragraph
used to name.** The row was split on 2026-08-22 (**D-055 §3**, approving N10). There are now three:

| Permission | Scope | Grants | Governs |
|---|---|---|---|
| **`ProjectCreate`** | `CompanyWide` | Owner, Technical Office | **opening** a project [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.ProjectCreate`] |
| **`ProjectManage`** | `ProjectScoped` | Owner, Technical Office | **editing** a project — §9's assignment requirement still applies to every edit [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.ProjectManage`] |
| **`ProjectFinancialsEdit`** | `ProjectScoped`, `TouchesMoney` | Owner, **Finance** | the contract's tax and financial settings **only** [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.ProjectFinancialsEdit`] |

**Do not merge them back.** Company-wide is the only instrument that reaches *opening*, because a
create request cannot name the project it is about to create and a `ProjectScoped` row returns
`ProjectNotSpecified` [Verified: 2026-08-22 @
`src/Domain/Authorization/PermissionEvaluator.cs` -> `ProjectNotSpecified`]. Widening `ProjectManage` instead would
fix creation **by removing §9's assignment requirement from every edit**, which is the hole the split
exists to avoid. And **the Finance department will never hold `ProjectManage`** — an accountant must
not alter the engineering scope of a project (D-055 §1).

> **Corrected 2026-08-22 under SM-29.** This paragraph said the permission *"as written can authorise
> editing and not opening — that is **N10**, for the Architect, and it is what blocks slice 4 now."*
> **N10 is approved and built**, and it also cited `PermissionCatalogue.cs` at lines 180-182, which is not
> where `ProjectManage` is. **Slice 4 is no longer blocked on a permission.** It is blocked on three
> workflow questions for Karim, all in `stories/questions-for-karim.md`: **Q-N10-1** (does opening a
> project put its creator on it), **Q-N10-2b** (Finance has no global reach, so Finance cannot set a
> new contract's withholding until somebody assigns Finance to it), and **Q-N10-3** (does opening a
> project need the Owner's approval — a state machine, not a permission).

**Slice 1 continues to assign against projects that arrive in seed data.** That is a test fixture, not
a business rule, and it must not become a habit: project creation is slice 4's, KAFF-407.

## Questions for Karim
None that block the endpoint. **Q17 is closed in full — the holder by D-052 §2, the scope residual by
D-055 §3. Q32 is closed (D-051). Q42 is closed (D-055 §2).** What is left touching this story:

- **Q43** — whether the project's reference code may appear beside its name on HR's picker. Open,
  does not block the endpoint.
- **The Q42 residual that is not a question:** `UserRead` is a permission, and the **projection** is
  the control. Recorded in the permissions bullet above rather than as a question, because nobody
  needs to rule on it — somebody needs to build it correctly.
- **Q-N10-1, Q-N10-2b, Q-N10-3** — slice 4, cited above, already registered. Not this story's.
