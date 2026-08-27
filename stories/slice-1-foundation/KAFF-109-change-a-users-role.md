# KAFF-109 · Change a user's role

**Slice:** 1 · **Epic:** Foundation · **Points:** 5 · **Status:** **REJECTED 2026-08-26** on `V-26-A` (a reachable `500` with no `messageKey`) and `V-26-B`. **Fixed `7ff500e` (D-088) and `f807364` (D-089).** **Not re-verified — not accepted.** Built `d5a1f87` (D-082). 🟡 D-088 leaves a question with Nabil and it is not this story's to answer: converting a user to `Role.Subcontractor` refuses today because refusing is the reversible half
**Spec:** §9 (2026-08-21 amendment, and the **⚠️ SUPERSEDED** block inside it) · **Decisions:** D-044 (rulings 1, 5), ~~D-049 (ruling 6)~~ **reversed by D-051 (Q27)**
**Depends on:** KAFF-106, KAFF-113, KAFF-111 *(shares the revocation mechanism)*

## Story
As the Owner, I change someone's role when their job changes — and the system takes them off every
project at the same moment, because a person's direct link to a site must not outlive the job that
put them there.

## Read this before you change anything here — the ruling reversed
**D-049 ruling 6 said a role change is *refused* while the user is an active Supervisor.** That is no
longer the rule. **D-051 (Q27) reverses it:**

> A role change **automatically revokes every project assignment they hold — Supervisor and Junior
> alike.** *"Their direct link to the site must be severed automatically to prevent lingering
> responsibilities. If they are needed on the project in their new capacity, HR must re-assign
> them."* — Karim, 2026-08-21 (second ruling), **D-051 Q27**

Both rulings weigh the same two risks — a headless site against a lingering liability — and land on
opposite sides. **The second is the answer.** The reversal is left visible in `spec.md` §9 as a
marked `⚠️ SUPERSEDED` block rather than edited away, precisely so that a future session does not
"correct" this story back to the first ruling. **If you are reading D-049 ruling 6 and this story
disagrees with it, this story is right.** Check `spec.md` §9 and D-051 before touching a rule here.

**What the reversal also closed.** A Site Engineer holds only `Junior` or `Supervisor` rows, never
`Standard` [Verified: 2026-08-22 @ `src/Domain/Identity/ProjectAssignment.cs` -> `Create`], so
blocking on Supervisor alone let a Junior-only
engineer through and left rows the domain would refuse to create. *"Whether Supervisor or Junior"*
covers it, and the mirror case — an office user with `Standard` rows becoming a Site Engineer — is
covered by the same mechanism: **revoke on any role change, then re-assign.** That was the whole of
Q27 and it is now closed.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | `UserManage` covers setting a user's role, Owner only | D-044 ruling 1 |
| 2 | **A role change revokes every active `ProjectAssignment` the user holds — every project, every level, `Supervisor`, `Junior` and `Standard` alike.** Nothing is refused, nothing is downgraded, nothing is kept | **D-051 (Q27)** · §9 ⚠️ SUPERSEDED block |
| 3 | Revocation goes through `ProjectAssignment.Revoke`, which **keeps the row as history** with its `RevokedAt` and revoking actor. Rows are never deleted [Verified: 2026-08-22 @ `src/Domain/Identity/ProjectAssignment.cs` -> `Revoke`, and `IsActive` is the computed `RevokedAt is null` — there is no stored active flag to get out of step] | **D-051 (Q27)** (*"`ProjectAssignment.Revoke` already does the right thing and keeps the row as history"*) · slice 0 |
| 4 | **This is the same revocation mechanism KAFF-111 uses for deactivation, and it is written once.** Two features needing the same thing means it lives in `Domain/` | CLAUDE.md (*"If two features need the same thing, it moves to `Domain/`"*) · KAFF-111 |
| 5 | Re-assignment is never automatic. If the person is still needed on the project in the new role, HR or the Owner assigns them again — a new row, today's date, a named author | **D-051 (Q27)** · KAFF-113 |
| 6 | **The response names every project the change took them off**, so that whoever has to re-assign them knows what to re-assign. Severing the link silently is how a site ends up unstaffed with nobody aware | **D-051 (Q27)** (*"HR must re-assign them"* presupposes HR is told which) |
| 7 | **`User.ChangeRole` is added to the entity** and owns the role transition and its own invariants (department compatibility, the client-id rule, the no-department rule for external roles). **It still does not exist** [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `class User` — the entity has `Create`, `SetOwnPassword`, `SetTemporaryPassword`, `ClearPassword`, `IsLockedOut`, `RecordFailedSignIn`, `RecordSuccessfulSignIn`, `Deactivate`, `Reactivate` and `MoveToDepartment`, and no role setter], so this is build work, not a description. It should reuse the private `ValidateDepartment` the way `MoveToDepartment` does rather than restate the rules. **The revocation is handler work**, because the rule needs assignment rows and the entity holds none. This closes finding **F-06**: the missing method is real, and it is not where the revocation goes | **D-051 (Q27)** (*"It still needs `ChangeRole` on `User` … the revocation is handler work because the entity cannot reach assignment rows"*) · F-06 |
| 8 | A change to the role the user already holds is not a change and revokes nothing. D-051 rules on *"a role change"*; where there is none there is nothing to sever | **D-051 (Q27)**, read for what it says |
| 9 | The role change takes effect on the next request, not at token expiry — the access policy re-reads the user row per request | D-048 · `meetings/2026-08-18-slice-1-kickoff.md` §3 |
| 10 | Seniority lives on the assignment, not on the user, which is why every level goes together — there is no level to carry across | D-044 ruling 5 |
| 11 | A role change re-applies every rule creation applies: department compatibility, the client-id rule for `Role.Client`, and the no-department rule for external roles | §9, §12 · D-035 · D-044 ruling 2 · KAFF-106, KAFF-107 |
| 12 | `errors.identity.role_change_blocked_by_supervision` is **withdrawn**. There is no supervision refusal any more, and a key left in the catalogue is a rule left in the product. Nothing to withdraw as it turns out — the key is in neither locale catalogue and no `IdentityErrors` member carries it [Verified: 2026-08-22 @ `src/Web/public/locales/ar.json`, `src/Web/public/locales/en.json`, `src/Domain/Identity/IdentityErrors.cs` — absent from all three]. **The rule is therefore: do not add it** | **D-051 (Q27)** |

## Permissions, money, audit, i18n
- **Permissions:** `UserManage`, `CompanyWide`, Owner only. HR cannot change a role even though HR
  can staff every project (D-044 ruling 2).
- **Money:** moves no money — and moves people into and out of every role that does. A user becoming
  `Role.Owner` acquires `FinancialMovementApprove` on every project (§7, §9), which makes this the
  single highest-consequence field in the system. It now also **removes** every project reach the
  person had, in one request.
- **Audit:** `Modified` on `User` with the old and the new role both present, **plus one `Modified`
  record per revoked `ProjectAssignment`**, each carrying its `ProjectId` so the trail filters per
  project (KAFF-116, KAFF-118). This is the record that answers *"who could approve that extract on
  the day it was approved"* and *"when did he come off site"*, and neither is answerable from the
  other. All of them are written in the same transaction as the role change.
- **i18n:** `enum.Role.*` for all nine roles, and `users.confirm.change_role.title` / `.body` /
  **`.revokes`** / `.reassign` — the confirmation **must** say that the person comes off every
  project, because the Owner will otherwise assume the opposite (the same reasoning as
  `users.confirm.reactivate.body`, KAFF-112). Plus the existing
  `errors.identity.hr_role_requires_hr_department`. Both catalogues.

  *(Corrected 2026-08-22 under **SM-15**, finding **V-07** / **N-05**. This bullet said `users.role.*`
  for the nine roles — a server enum rendered as text is `enum.<Type>.<Member>`
  [Verified: 2026-08-22 @ `ux/rtl-and-i18n.md` -> `enum.<Type>.<Member>`] — and
  `users.role.change_revokes_assignments_notice` for the warning, which S-008's confirm dialog draws as
  `users.confirm.change_role.revokes`
  [Verified: 2026-08-22 @ `ux/slice-1-flows.md` -> `users.confirm.change_role.revokes`]. Neither
  spelling is in either catalogue yet.)*

## Acceptance criteria
**AC-109-A — a supervisor comes off site, and is not refused** *(fails if the rule is broken)*
Given a Site Engineer who is Supervisor on project A
When the Owner changes their role to Technical Office
Then the change succeeds
And their assignment to project A is revoked
And the response names project A

**AC-109-B — junior assignments go too** *(fails if the rule is broken — this is the half D-049 left open)*
Given a Site Engineer who is Junior on projects A, B and C and Supervisor on none
When the Owner changes their role to Technical Office
Then all three assignments are revoked, and the response names all three

**AC-109-C — the mirror case** *(fails if the rule is broken)*
Given a Finance user holding two `Standard` assignments
When the Owner changes their role to `Role.SiteEngineer`
Then both `Standard` rows are revoked
And no row the domain would refuse to create is left behind — a scan of active assignments finds no `Standard` row held by a `SiteEngineer` and no `Junior`/`Supervisor` row held by anyone else

**AC-109-D — history survives** *(fails if the rule is broken)*
Given the same user after the change
When the assignment table is read
Then every revoked row is still present with its original `AssignedAt`, its `AssignedByUserId` and a `RevokedAt` of today — nothing was deleted and nothing was rewritten

**AC-109-E — nothing is restored**
Given the same user, now Technical Office
When they call any endpoint on project A
Then it is refused with 403 and `errors.auth.forbidden`
And putting them back requires HR to create a **new** assignment row (KAFF-113)

**AC-109-F — a role change takes effect immediately** *(fails if the rule is broken)*
Given a Finance user holding a session opened before the change
When the Owner changes their role to Technical Office
And they call an endpoint requiring `TreasuryPostProject` with that same session
Then the request is refused with 403

**AC-109-G — the department rules are re-applied**
Given a Marketing user in `Department.Marketing`
When the Owner changes their role to `Role.Hr` without moving their department
Then it is refused with `errors.identity.hr_role_requires_hr_department`
And **no assignment is revoked** — a refused change is not a change

**AC-109-H — a change to the same role does nothing** *(fails if the rule is broken)*
Given a Site Engineer who is Supervisor on project A
When the Owner sets their role to `Role.SiteEngineer`, which they already hold
Then the assignment to project A is still active

**AC-109-I — only the Owner may**
Given I am HR, which can staff projects
When I attempt to change any user's role
Then it is refused with 403, and no assignment is revoked

**AC-109-J — the before-state and every revocation are in the trail** *(fails if the rule is broken)*
Given a role change that revoked three assignments
When the audit trail is read
Then it names the actor, the old role and the new role, and carries three `ProjectAssignment` records, each naming its project

**AC-109-K — it is one transaction** *(fails if the rule is broken)*
Given a role change that would revoke three assignments, where the third revocation fails
When the request completes
Then the role is unchanged and all three assignments are still active — there is no state in which the role moved and the assignments did not

## Not in this story
Department moves (KAFF-108). Deactivation (KAFF-110) and the revocation it performs (KAFF-111) —
**note that the two acts now behave the same way**, which they did not under D-049 ruling 6, and that
the shared mechanism is the point of rule 4. Re-assigning the person afterwards (KAFF-113). What a
role change does to approvals already in flight — there are no movements in slice 1; the question
returns in slice 5 and must be raised again there.

## Questions for Karim
None that block. **Q41** — whether an internal staff account may be converted *into* a `Role.Client`
portal login at all — touches rule 11 rather than changing it: the creation-time invariants (§9, §12,
D-035) already govern the transition, and nothing in any source forbids or permits it as a deliberate
act. Raised because a staff account becoming a client's portal identity keeps a staff person's audit
history under a client-facing login, and nobody has been asked whether that is wanted.
