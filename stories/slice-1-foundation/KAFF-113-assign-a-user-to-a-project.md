# KAFF-113 · Assign a user to a project, with seniority for site engineers

<!-- kaff id=KAFF-113 slice=1 points=5 state=VERIFIED verdict=CONDITIONAL at=91cdcb3 on=2026-11-17 -->

**Rewritten 2026-11-18** to Nabil's rulings D-217 item 36, D-227, D-228, D-229, D-233, D-234 (Verifier finding, `qa/release-1/verification-2026-11-17.md`). State is in the trailer.

**Slice:** 1 · **Epic:** Foundation · **Points:** 5 · **Status:** ACCEPTED 2026-08-26, standing. No commit since has touched this story's own code
**Spec:** §9, §8 · **Decisions:** D-010, D-012, D-044 (rulings 3, 5), D-035
**Depends on:** KAFF-106

## Story
As the Owner, I put a person on a project, and choose whether a site engineer is Supervisor or
Junior on **this** project — because the same engineer can be a supervisor on one site and a junior
on another.

This is the second half of `spec.md` §9's *"Permission = role × assignment"*. Without a row here a
correctly-roled user is refused, which is the rule the whole permission model rests on.

> Changed 2026-11-18 (D-233 item 3). The actor was "the Owner or HR". **Only the Owner** adds people to a
> project; HR lost `ProjectAssignmentManage`. The add path is the sheet on the project's team tab (mock
> `.design/v2/team-a/project-team.html`, approved D-229 item 1), which reads
> `GET /api/projects/{id}/assignments/candidates` for the people who can be added.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | `ProjectAssignmentManage` is held by `Role.Owner` **alone**. > Changed 2026-11-18 (D-233 item 3): it was Owner and HR (§9 · D-012 · D-044 rulings 1, 2). HR cannot open a project, never reached the team tab, and loses the grant | D-233 item 3 |
| 2 | ~~HR assigns on any project that exists, with no assignment row of its own.~~ > Removed 2026-11-18 (D-233 item 3). HR no longer assigns, so the "circular" argument (D-044 ruling 3) has no actor. The first assignments on a new project are made by the system, see rules 11 and 12 | D-233 item 3 |
| 3 | The Owner reaches every project that exists, without an assignment row | D-010 · D-044 ruling 3 |
| 4 | The Owner's reach stops at a project that does not exist. The permission stays `ProjectScoped`, so the route must still name a real project [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.ProjectAssignmentManage`; @ `src/Infrastructure/Authorization/ProjectAccessPolicy.cs` -> `GlobalReachAsync`]. > Changed 2026-11-18 (D-233): the rule named HR; it holds for the Owner | D-044 ruling 3 |
| 5 | Seniority lives on the assignment, not on the user. An engineer can be a Supervisor on one project and a Junior on another | D-044 ruling 5 |
| 5a | A project may have **any number** of supervising engineers | D-228 item 1 |
| 6 | A level other than `Standard` is only legal for `Role.SiteEngineer`; `Standard` is only legal for everyone else. §9 attaches Junior/Supervisor to the site engineer role alone. On the sheet the level choice («مبتدئ» / «مشرف») is shown **only** when the chosen person is a site engineer, and is then required [Verified: 2026-08-22 @ `src/Domain/Identity/ProjectAssignment.cs` -> `Create`] | §9 · D-229 item 1 |
| 7 | `Role.Client` and `Role.Subcontractor` are never assigned. A portal user reaches a project through `Project.ClientId` matching `User.ClientId`, never through an assignment row [Verified: 2026-08-22 @ `src/Infrastructure/Authorization/ProjectAccessPolicy.cs` -> `ClientAccessAsync`] | §9, §12 · D-035 |
| 7a | **`Role.Hr` is never assigned** (`identity.hr_is_not_assignable`) and is not listed among the people who can be added. HR's reach is global and an assignment gave it nothing | D-234 item 2 · D-233 item 3 |
| 8 | An inactive user is not assignable, and an assignment does not resurrect one | §9 · KAFF-110 |
| 9 | A user may hold only one active assignment per project. Re-assignment after removal is legal, **for anyone removed, including the creator** — the unique index covers active rows only | slice 0 `ProjectAssignment` · D-228 item 1 |
| 10 | The row records who assigned and when | slice 0 `ProjectAssignment` |
| 11 | **Whoever creates a project is assigned to it** in the same save. Finance users are added **automatically** to every new project | D-227 item 1 · D-217 item 36 |
| 12 | **Whoever converts a design project is assigned to the execution project.** Finance is **not yet** staffed on a converted project — a known defect (D-228 "Built under D-227"); the conversion is not this story's, see "Not in this story" | D-228 |
| 13 | How a member joined (`origin`: automatic, creator, added) is derived from the stored timestamps, not stored as a flag. A creator who is removed and added back reads as **added** | D-233 "Built before this ruling" |

## Permissions, money, audit, i18n
- **Permissions:** `ProjectAssignmentManage`, `ProjectScoped`, `Role.Owner` only. Every other role, HR
  included, is refused 403. **HR reaching a project does not mean HR can read it**: HR is deliberately
  absent from `ProjectRead` (D-044 ruling 2) — unchanged.
  > Changed 2026-11-18 (D-233 item 3). This bullet described HR's global reach, its picker (Q32, Q42,
  > D-051, D-055 §2) and a separate HR "Project Team" screen. HR no longer assigns, so none of it applies
  > to this story. `UserRead` for HR (D-055 §2) is unchanged and belongs to the people screens, not here.
- **Candidates read:** `GET /api/projects/{id}/assignments/candidates` lists who can be added. Gated like
  the assign route. It returns names and roles only (and whether the person is a site engineer, so the
  sheet knows to ask for a level) — **never the full user row** (usernames, departments, active state):
  the projection is the control.
- **Money:** moves no money. It decides who can later prepare, gate, approve and disburse on this project.
- **Audit:** `Created` on `ProjectAssignment` with `ProjectId` set, so the trail filters per project,
  actor = the Owner (or the system for the automatic Finance rows). KAFF-116 adds *how* the actor reached
  the project. A removed person's past work stays under his name through the trail (D-228 item 1).
- **i18n:** `assignments.action.assign`, `assignments.assign.title`, `assignments.field.user`,
  `assignments.field.level`, `assignments.hint.level_per_project`,
  `enum.AssignmentLevel.Standard` / `.Junior` / `.Supervisor`, and the existing
  `errors.identity.assignment_level_not_applicable`, `errors.identity.client_is_not_assignable`,
  plus `identity.hr_is_not_assignable`. No project code and no database code on the sheet (no-codes rule).

## Acceptance criteria
**AC-113-A — HR cannot staff a project** *(fails if the rule is broken)*
> Changed 2026-11-18 (D-233 item 3). Was "HR staffs a project it was never assigned to ... the assignment is created". It is now the opposite.

Given I am `Role.Hr`, with or without any assignment row
When I attempt to assign a Technical Office user to a project
Then I am refused with 403 and no assignment row is created

**AC-113-B — and still cannot open that project** *(fails if the rule is broken)*
Given I am the same HR user
When I call an endpoint requiring `ProjectRead` on that project
Then I am refused with 403
> Holds as written. The Verifier confirmed it (2026-11-17).

**AC-113-C — the Owner's reach stops at a project that does not exist**
> Changed 2026-11-18 (D-233 item 3). Actor was HR.

Given I am `Role.Owner`
When I assign a user to a project id that does not exist
Then the request is refused, and the refusal is not a 500

**AC-113-D — the same engineer, two seniorities**
Given a Site Engineer
When the Owner assigns them Supervisor on project A and Junior on project B
Then both rows exist with their own levels, and each row's `AssignmentLevel` is the one it was given
And the permission evaluator grants `DraftSubmit` on A and refuses it on B, asserted against
`PermissionEvaluator` directly
> Changed 2026-11-18 (D-233): actor HR became Owner. The 2026-08 note stands: the evaluator is asserted, not `/api/auth/me` (per-project permissions are KAFF-105b's).

**AC-113-E — seniority is refused where §9 does not put it** *(fails if the rule is broken)*
> Changed 2026-11-18 (D-233 item 3). Actor was HR.

Given a Finance user
When the Owner assigns them with level Supervisor
Then it is refused with `errors.identity.assignment_level_not_applicable`
And when a Site Engineer is assigned with level `Standard`, that is refused too

**AC-113-F — clients and subcontractors are not assignable** *(fails if the rule is broken)*
> Changed 2026-11-18 (D-233 item 3). Actor was HR.

Given a `Role.Client` user and a `Role.Subcontractor` user
When the Owner attempts to assign either to a project
Then both are refused with `errors.identity.client_is_not_assignable`

**AC-113-G — nobody else can staff a project** *(fails if the rule is broken)*
> Changed 2026-11-18 (D-233 item 3). HR joins the refused list.

Given I am HR, then Finance, then Technical Office, then a Supervisor Site Engineer assigned to the project
When each attempts to assign a user to it
Then every one is refused with 403 — being on the project is not permission to staff it

**AC-113-H — an inactive user is not assignable**
> Changed 2026-11-18 (D-233 item 3). Actor was HR.

Given a deactivated user
When the Owner attempts to assign them
Then it is refused

**AC-113-I — no duplicate active assignment**
> Changed 2026-11-18 (D-233 item 3). Actor was HR.

Given a user already assigned to project A
When the Owner assigns them to project A again
Then it is refused; and after the first is removed, a new assignment succeeds

**AC-113-J — HR is not assignable** *(fails if the rule is broken)* — added 2026-11-18 (D-234 item 2)
Given an active `Role.Hr` user
When the Owner attempts to assign them to a project
Then it is refused with `identity.hr_is_not_assignable`, and no row is created

**AC-113-K — the candidates read lists only people who can be added** *(fails if the rule is broken)* — added 2026-11-18 (D-233, D-234 item 2)
Given project A has an active Site Engineer, an active Finance user, an inactive user, a `Role.Client` user, a `Role.Subcontractor` user, an active `Role.Hr` user, and one person already active on A
When the Owner reads `GET /api/projects/A/assignments/candidates`
Then it lists the active Site Engineer and the active Finance user who are not on A
And it does not list the inactive user, the client, the subcontractor, the HR user or the person already on A
And every item carries a name and role and no username, department or active flag
And a non-Owner caller is refused 403

**AC-113-L — the sheet asks for seniority only for a site engineer** — added 2026-11-18 (D-229 item 1)
Given the Owner opens the add sheet on the team tab at 390px in Arabic
When he picks a Finance user, then picks a Site Engineer
Then the level choice («مبتدئ» / «مشرف») is absent for the first and shown, required, for the second
And pressing «إضافة» with a Site Engineer and no level marks the missing choice and does not submit
And when no candidate exists the sheet says «لا يوجد أحد متاح للإضافة»

**AC-113-M — the creator and Finance are assigned when a project is created** — added 2026-11-18 (D-227 item 1, D-217 item 36)
Given a Technical Office user and two active Finance users
When the Technical Office user creates a project
Then in the same save the creator and both Finance users hold active assignments on it
And the Finance rows read `origin` automatic and the creator's row reads `origin` creator

**AC-113-N — a removed person can be added back; the creator too** — added 2026-11-18 (D-228 item 1)
Given a person removed from project A (the creator, in one run)
When the Owner adds them again
Then a new active row exists, the removed row is untouched, and the team read shows them as added

**AC-113-O — any number of supervising engineers** — added 2026-11-18 (D-228 item 1)
Given project A with one Supervisor Site Engineer
When the Owner assigns two more Site Engineers as Supervisor
Then both succeed

## Not in this story
Removing (KAFF-114). Displaying the team (KAFF-115). Creating a project — slice 4, KAFF-407; this story
only states what creating one does to the team (rule 11, AC-113-M).

> Changed 2026-11-18. **Design-phase conversion:** staffing the execution project when a design project
> converts (rule 12) is not specified or accepted here; Finance is not yet staffed on a converted project
> (D-228), a defect for its own lane. **The HR team screen** (S-009b, `ProjectTeamRead`) and **HR's picker
> for assigning** are out (D-233 item 3). **A per-person history screen** ("what did he do on this
> project") is not built; the audit trail (KAFF-117, Owner only) is the history.

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

## Questions for Nabil
Added 2026-11-18. Each has a recommended answer; nothing was invented in the criteria above.
1. **Q-113-1 — a project created when no Finance user is active.** Rule 11 adds Finance automatically; with no active Finance user the team has no Finance row (the Verifier found this, 2026-11-17). Recommended: creation succeeds, and the Owner adds Finance on the sheet later. Not built as a rule.
2. **Q-113-2 — a Finance user created after the project.** Is he added to existing projects automatically? Recommended: no, the Owner adds him on the sheet.
3. **Q-113-3 — may the Owner add himself?** Recommended: no, he already reaches every project.
4. **Q-113-4 — may the Owner add any other role** (Marketing, Technical Office, Finance) the rules above allow? Candidates follow rules 6-8 only. Recommended: yes.

Closed, no longer this story's: Q17, Q32 (D-051), Q42 (D-055 §2), Q43 (D-100) all concerned HR's picker
and screen, removed under D-233 item 3. Q-N10-1 is answered by D-227 item 1, Q-N10-2b by D-217 item 36.
Q-N10-3 is slice 4's.
