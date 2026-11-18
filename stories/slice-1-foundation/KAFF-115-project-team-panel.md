# KAFF-115 · The project team tab is built from assignment rows, not from the access check

<!-- kaff id=KAFF-115 slice=1 points=8 state=BUILT verdict=REJECTED at=91cdcb3 on=2026-11-17 -->

**Slice:** 1 · **Epic:** Foundation · **Points:** 8 (unchanged) · **Rewritten 2026-11-18** to Nabil's rulings D-227 item 2, D-228 item 1, D-229 item 1, D-233 item 3, D-234 (Verifier finding, `qa/release-1/verification-2026-11-17.md`: AC-F, H, I not met). State is in the trailer.
**Spec:** §9 · **Decisions:** D-010, D-044 (rulings 2, 3), D-217 item 36, D-227, D-228, D-229, D-233, D-234. ~~D-051 (Q32), D-100 (Q43)~~ — see rules 5 and 8
**Depends on:** KAFF-113, KAFF-114
**Built mock:** `.design/v2/team-a/project-team.html` (approved, D-229 item 1)

> Changed 2026-11-18. This story was a read-only panel plus a separate HR "Project Team" screen. It is now
> the **team tab** inside a project: current members, how each joined, and, for the Owner alone, the add
> and remove controls (KAFF-113, KAFF-114). Everyone else reads it and can change nothing. The history of
> the old Status paragraph (blocks, re-estimates, the KAFF-105b dependency and `ProjectTeamRead`) is in git.

## Story
As anyone who can open a project, I see who is on its team, how each person joined, and, if I am the
Owner, I can add and remove people. The list is the assignment rows, not "everybody the access check
would let in", because the second list contains the Owner on every project in the company.

Raised at the kickoff, BA to UX: *"build the project team panel from `ProjectAssignment` rows, never
from the access check"* (`meetings/2026-08-18-slice-1-kickoff.md` §4).

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | The team is the set of **active** `ProjectAssignment` rows for the project. It lists current members only | §9 · D-228 item 1 |
| 2 | The Owner reaches every project without an assignment row and appears on no team unless somebody actually assigned him. **The access check can never be the source of this tab** [Verified: 2026-11-17 @ `GetProjectTeam/Handler.cs`: rows only]. HR also reaches without a row, and cannot be assigned, so HR is on no team (D-234 item 2) | D-010 · D-044 ruling 3 · D-234 |
| 3 | Each member shows name, role and, for site engineers, the level («مبتدئ» / «مشرف»), because seniority is per project | §9 · D-044 ruling 5 |
| 4 | Removed members are not in the tab. Their past work stays under their name through the audit trail | D-228 item 1 |
| 5 | ~~The team is reachable from two surfaces: staff with `ProjectRead`, and HR on its own "Project Team" screen with `ProjectTeamRead`.~~ > Removed 2026-11-18 (D-233 item 3). HR cannot open a project and has no team screen. There is one surface, the tab, read with `ProjectRead` and assignment | D-233 item 3 |
| 5a | **HR is not granted `ProjectRead`**, and a route that gives HR the project is a defect. Holds unchanged (D-044 ruling 2) | D-044 ruling 2 |
| 5b | ~~Two separate response types so a filtered view cannot leak.~~ > Changed 2026-11-18 (D-233): with no HR surface the team type is simply never the project dashboard's type and never carries money, see AC-115-I | §12 · D-035 |
| 6 | A portal client never sees it | §12 |
| 7 | A deactivated member is absent because deactivation revokes their assignments (D-049 ruling 5), not because the tab filters on `IsActive` | D-049 ruling 5 · KAFF-111 |
| 8 | ~~The name line is rendered `[RefCode] Project Name`.~~ > Removed 2026-11-18 (D-233 / no-codes rule: no code on a screen). The tab names people, not the project | D-233 · memory: screens explain themselves |
| 9 | ~~HR's team-size figure is not on this panel.~~ > Removed 2026-11-18 (D-233 item 3): HR has no team screen. HR's list figure remains KAFF-125's | D-233 item 3 |
| 10 | **Each row shows how the person joined** (`origin`): «تلقائي» for an automatic Finance row, «أنشأ المشروع» for the creator, nothing for a person who was added. A creator who was removed and added back reads as added | D-233 "Built before this ruling" · D-229 item 1 |
| 11 | **Each row says whether the caller may remove him** (`canRemove`), and the read says whether the caller may manage at all (`canManage`). `canManage` is true only for the Owner. `canRemove` is false on every Finance row and false on every row when `canManage` is false. The server decides; the screen only reflects it | D-233 item 3 · D-228 item 1 |
| 12 | Read-only for everyone but the Owner: no add button, no remove button | D-233 item 3 |
| 13 | Add is a sheet with a person select and, for a site engineer only, the level choice («مبتدئ» / «مشرف»), required. Remove asks first (KAFF-114 rule 8). Both end with a short notice, «أُضيفت {name}» / «أُزيل {name}», with no project code and no database code on screen | D-229 item 1 · KAFF-113 · KAFF-114 |
| 14 | On an overview with no site engineer the Owner's primary is «إضافة شخص»; the Technical Office sees «لم يُسنَد مهندس الموقع بعد» with no primary | D-229 item 1 |

## Permissions, money, audit, i18n
- **Permissions:** the read is `ProjectRead`, `ProjectScoped`: assignment required for everyone except
  the Owner, whose global reach is a reach rule (D-010). `GET /api/projects/{id}/team` returns
  `{ canManage, members[] }`. Add and remove are `ProjectAssignmentManage`, Owner only (KAFF-113, KAFF-114).
  > Changed 2026-11-18 (D-233 item 3). `ProjectTeamRead` and the HR surface are dropped; whether the
  > unused permission row is deleted is Q-115-1.
- **Money:** moves no money and shows no money: no rate, no cost, no salary.
- **Audit:** the read writes none. Add and remove write theirs (KAFF-113, KAFF-114).
- **i18n:** `team.title`, `team.empty`, `team.member.role`, `team.member.level`, `team.origin.auto`
  («تلقائي»), `team.origin.creator` («أنشأ المشروع»), `team.action.add`, `team.action.remove`, reusing
  `enum.Role.*` and `enum.AssignmentLevel.*`. `team.screen.title` (HR's surface) is dropped.

## Acceptance criteria
**AC-115-A — the Owner is not on every team** *(fails if the rule is broken)*
> Changed 2026-11-18 (D-217 item 36). "Exactly one member" can no longer happen: Finance is staffed automatically. Restated; the intent holds.

Given an Owner who has never been assigned to project A, and one Technical Office user who has
When project A's team is read
Then the Technical Office user is in it and the Owner is not, and no row exists for the Owner

**AC-115-B — nor is HR** *(fails if the rule is broken)*
> Changed 2026-11-18 (D-233 item 3, D-234 item 2). The given ("HR who assigned everyone") can no longer happen. Restated: HR cannot be on a team at all.

Given an HR user who reached project A and holds no assignment row
When A's team is read as the Owner
Then HR is not in it
And the Owner's attempt to add that HR user is refused (`identity.hr_is_not_assignable`, KAFF-113 AC-113-J)

**AC-115-C — seniority shows, per project**
Given a Site Engineer who is Supervisor on A and Junior on B
When A's team and B's team are read
Then each shows the level for that project
> Holds as written (Verifier: MET).

**AC-115-D — removed members are gone**
Given a member whose assignment was removed
When the team is read
Then they are absent
> Holds as written (Verifier: MET). A re-added person reads as added.

**AC-115-E — and a leaver is gone the same way** *(fails if the rule is broken)*
Given a team of two, one of whom the Owner then deactivates
When the team is read, and then the audit trail for that project
Then the team shows one member, and the trail still shows both and when the second came off (D-049 ruling 5)
> Holds as written. Verifier: UNTESTED, to be covered by a test.

**AC-115-F — an empty team has an explicit empty state**
> Holds as written (Verifier: NOT MET at `project-team-tab.html:28-43`). The empty state occurs when a project is created while no Finance user is active (Q-113-1) and the creator was later removed.

Given a project with no active assignments
When the team tab is read as the Owner, and as a user with `ProjectRead` and no manage right
Then it renders `team.empty` — never a blank area and never a phantom row
And the Owner still sees «إضافة شخص»; the other reader sees no button

**AC-115-G — a client cannot read it** *(fails if the rule is broken)*
Given I am a `Role.Client` user whose client owns project A
When I request project A's team
Then I am refused before any handler runs — `Role.Client` may never hold a staff session [Verified: 2026-09-02 @ `src/Domain/Identity/Role.cs` -> `MayHoldStaffSession`]
> Holds as written (Verifier: MET).

**AC-115-H — HR is refused and reaches nothing** *(fails if the rule is broken)*
> Changed 2026-11-18 (D-233 item 3). Was "HR reads the team on its own Project Team screen as `[RefCode] Project Name`". The HR screen and the `[RefCode]` format are removed (no-codes rule).

Given I am `Role.Hr` with no assignment row, and project A has a `ContractValue` set
When I request `GET /api/projects/A/team`
Then I am refused 403
And `Role.Hr` holds no `ProjectRead` grant in the permission catalogue, and by no global-reach rule either

**AC-115-I — the team type carries people, never money** *(fails if the rule is broken)*
> Changed 2026-11-18 (D-233 item 3). Was "two distinct types for two surfaces, limited to name, code, member name, role and level". With one surface the limit moves to what the tab needs, and the money exclusion stays.

Given the team read's response type (`TeamResponse` / `TeamMember`) and the project dashboard's type
When a test enumerates every public property of each type by reflection
Then the two are distinct CLR types
And the team types carry no property outside {`canManage`; per member: `assignmentId`, `userId`, name, role, level, `origin`, `canRemove`, `assignedAt`}
And the test fails the moment a financial field (value, cost, rate, margin, balance, status, client) or a project code is added to either team type

**AC-115-J — Arabic, RTL, at mobile width**
Given the tab at 390px in Arabic
When it renders
Then direction is RTL, names are bidi-isolated, and there is no horizontal overflow
> Holds as written (Verifier: MET).

**AC-115-K — each row says how the person joined** — added 2026-11-18 (D-233 "Built before this ruling")
Given project A created by a Technical Office user, with an active Finance user added automatically, and an engineer added by the Owner
When the Owner reads A's team
Then the creator's row reads `origin` creator and shows «أنشأ المشروع», the Finance row reads automatic and shows «تلقائي», and the engineer's row shows neither
And when the creator is removed and added back, his row reads as added and shows neither mark

**AC-115-L — `canRemove` and `canManage` follow the caller and the row** *(fails if the rule is broken)* — added 2026-11-18 (D-233 item 3, D-228 item 1)
Given project A with a creator, a Finance user and a Site Engineer
When the Owner reads the team
Then `canManage` is true, the Finance row has `canRemove` false, and the creator and engineer rows have `canRemove` true
And when a Technical Office user and a Site Engineer on A each read it, `canManage` is false and every row has `canRemove` false and the screen shows no add or remove button

**AC-115-M — the add sheet and the candidates read** — added 2026-11-18 (D-229 item 1)
Given the Owner on the team tab
When he opens «إضافة شخص»
Then the sheet lists the people from `GET /api/projects/A/assignments/candidates` and no one else, and shows the level choice only for a Site Engineer (KAFF-113 AC-113-K, AC-113-L)
And after adding, the team shows the new member and the notice «أُضيفت {name}»

**AC-115-N — Finance has no remove button** — added 2026-11-18 (D-228 item 1)
Given the Owner on the team tab of a project with a Finance member
When the tab renders
Then the Finance row has no «إزالة» button and the other rows have one

**AC-115-O — remove confirms and refreshes** — added 2026-11-18 (D-229 item 1)
Given the Owner on the team tab
When he removes a Site Engineer through the confirmation (KAFF-114 AC-114-I)
Then the row leaves the list, the notice «أُزيل {name}» shows, and the person appears again in the candidates

## Not in this story
Adding and removing as actions (KAFF-113, KAFF-114). Who assigned whom and when: the audit trail,
KAFF-117, the Owner's alone (D-049 ruling 1) — **nobody reading this tab can also read the history
behind it unless they are the Owner.**

> Changed 2026-11-18. **Out of this story:** the HR Project Team screen (S-009b) and `ProjectTeamRead`
> (D-233 item 3); HR's project list and team-size figure (S-009a, KAFF-125); **design-phase conversion**
> (who is staffed on the converted project, and the Finance gap, D-228); **a per-person history screen**
> ("what did he do on this project"): the trail is the history; a team tab on a design project's states
> is the project overview's, not this story's.

## Questions for Nabil
Added 2026-11-18. Recommended answers are mine, not rulings.
1. **Q-115-1 — the `ProjectTeamRead` permission row.** The HR screen it served is gone and no route uses it. Recommended: delete the row and its test together, in one change, so it is not an unused grant.
2. **Q-115-2 — who besides the Owner may read the tab?** Today it is anyone with `ProjectRead` and an assignment (Technical Office, Finance, site engineers). Recommended: keep that, read-only.
3. **Q-115-3 — should a site engineer see the other members' roles and levels?** Recommended: yes, no money is shown.
4. **Q-115-4 — what the empty state offers a non-Owner** (AC-115-F). Recommended: the text `team.empty` only.

Closed and no longer this story's: Q32 (D-051) and Q43 (D-100), which concerned the HR screen and the `[RefCode]` format, both removed under D-233 item 3 and the no-codes rule.
