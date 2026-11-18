# KAFF-114 · Revoke a project assignment without losing who could act when

<!-- kaff id=KAFF-114 slice=1 points=3 state=VERIFIED verdict=CONDITIONAL at=91cdcb3 on=2026-11-17 -->

**Rewritten 2026-11-18** to Nabil's rulings D-227, D-228, D-229, D-233, D-234 (Verifier finding, `qa/release-1/verification-2026-11-17.md`). State is in the trailer. "Revoke" is the code's word; the screen says remove («إزالة»).

**Slice:** 1 · **Epic:** Foundation · **Points:** 3 · **Status:** ACCEPTED 2026-08-26, standing. No commit since has touched this story's own code. `TC-1-120` is still uncovered — QA → Backend, P2
**Spec:** §9, §7 · **Decisions:** D-044 (ruling 3)
**Depends on:** KAFF-113

## Story
As the Owner, I take someone off a project, from the team tab, and the record of them having been on it survives —
so that six months later the trail can still answer who was allowed to act on the day an extract was
approved.

> Changed 2026-11-18 (D-233 item 3). The actor was "the Owner or HR". **Only the Owner** removes people;
> HR lost `ProjectAssignmentManage`. The remove button sits on the person's row in the team tab (mock
> `.design/v2/team-a/project-team.html`, approved D-229 item 1) and asks first, see rule 8.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | `ProjectAssignmentManage`, `Role.Owner` alone, with global reach. > Changed 2026-11-18 (D-233 item 3): was Owner and HR (§9 · D-012 · D-044 rulings 1, 3) | D-233 item 3 |
| 2 | Revocation is a soft close: `RevokedAt` and `RevokedByUserId` are stamped and the row stays. It is never a delete. `IsActive` is the computed `RevokedAt is null`, so there is no stored flag that can drift from the timestamp [Verified: 2026-08-22 @ `src/Domain/Identity/ProjectAssignment.cs` -> `Revoke`, `RevokedAt`, `IsActive`] | slice 0 `ProjectAssignment` |
| 3 | Access ends on the next request. The assignment is read per request, not baked into the token — the access policy queries `RevokedAt == null` on every project-scoped decision [Verified: 2026-08-22 @ `src/Infrastructure/Authorization/ProjectAccessPolicy.cs` -> `AssignedAccessAsync`] | §9 (*"Enforcement is server-side"*) |
| 4 | Revoking an already-revoked assignment is refused — `errors.identity.assignment_already_revoked` [Verified: 2026-08-22 @ `src/Domain/Identity/ProjectAssignment.cs` -> `Revoke`; @ `src/Domain/Identity/IdentityErrors.cs` -> `AssignmentAlreadyRevoked`]. ⚠️ **UNCITED — WAIVED, Q51. See "Readiness waiver" below** | slice 0 `ProjectAssignment.Revoke` |
| 5 | Revocation withdraws nothing the person did. Anything they created stands, and corrections are new movements, never edits | CLAUDE.md (*append-only*) · §7 |
| 6 | After revocation the same user may be assigned again — the unique index covers active rows only | slice 0 `ProjectAssignment` |
| 7 | Removing the last engineer on a project is allowed. Nothing in `spec.md` requires a project to have anyone on it, and inventing a minimum would be a rule nobody asked for. D-228 item 1 confirms there is no cap on supervising engineers; it does not set a minimum either. ⚠️ **UNCITED — WAIVED, Q49. See "Readiness waiver" below** | §9 — absence noted deliberately |
| 8 | **Remove asks first.** Pressing «إزالة» on a row opens a confirmation, «إزالة {name} من المشروع؟», with the line that he loses access and his past work stays and that he can be added again, and two buttons, «إزالة» and «إلغاء». Cancelling changes nothing. The confirmation is recoverable: nothing is lost by removing, because the person can be added back | D-229 item 1 · D-228 item 1 |
| 9 | **Finance cannot be removed.** The row of a `Role.Finance` user is refused (`errors.identity.finance_cannot_be_removed`), by the server, and the team read marks it `canRemove: false` so the screen shows no remove button on it | D-228 item 1 · D-233 "Built before this ruling" |
| 10 | **The creator can be removed** by the Owner. Anyone removed can be added back (KAFF-113 rule 9) | D-228 item 1 |
| 11 | **A removed person's past work stays under his name** through the audit trail. The team tab lists current members only | D-228 item 1 |
| 12 | Only the Owner is offered the remove button. For every other reader the team read says `canManage: false` and no row says `canRemove: true` (KAFF-115) | D-233 item 3 |

## Permissions, money, audit, i18n
- **Permissions:** `ProjectAssignmentManage`, `ProjectScoped`, `Role.Owner` only (D-233 item 3). Every other role, HR included, is refused 403.
- **Money:** moves no money.
- **Audit:** `Modified` on `ProjectAssignment`, `ProjectId` set, `ChangedProperties` naming
  `RevokedAt` and `RevokedByUserId`, actor = the Owner.
- **i18n:** `assignments.action.revoke`, `assignments.confirm.revoke.title` / `.body`,
  `a11y.revoke_assignment`, `assignments.revoked_on`, `action.confirm`, `action.cancel`, and the
  existing `errors.identity.assignment_already_revoked`, plus `errors.identity.finance_cannot_be_removed`. The screen's word is remove («إزالة»). No database code on the screen.

  *(Corrected 2026-08-22 under **SM-15**, finding **V-07** / **N-05**. This bullet said
  `assignments.revoke` and `assignments.revoke.confirm`; S-010 draws them as
  `assignments.action.revoke` and `assignments.confirm.revoke.title` / `.body`
  [Verified: 2026-08-22 @ `ux/slice-1-flows.md` -> `assignments.confirm.revoke.title`], which is also
  the `<feature>.action.*` / `<feature>.confirm.*` shape of `ux/rtl-and-i18n.md` §6
  [Verified: 2026-08-22 @ `ux/rtl-and-i18n.md` -> `<feature>.confirm.*`]. `assignments.revoked_on` is
  kept: it is a plain label and §6 has no narrower shape for one. Neither spelling is in either
  catalogue yet.)*

## Acceptance criteria
**AC-114-A — access ends on the next request** *(fails if the rule is broken)*
> Changed 2026-11-18 (D-233 item 3). Actor was HR. The behaviour held with the Owner (Verifier, chain A).
Given a Site Engineer assigned to project A, holding a valid token, who has just written a daily-log-style request successfully
When the Owner removes the assignment
And the engineer repeats the identical request with the same token
Then it is refused with 403 and `errors.auth.forbidden`

**AC-114-B — the row survives** *(fails if the rule is broken)*
> Holds as written.
Given a revoked assignment
When the assignment table is read
Then the row is still there with `AssignedAt`, `AssignedByUserId`, `RevokedAt` and `RevokedByUserId` all populated

**AC-114-C — re-assignment is legal**
> Changed 2026-11-18 (D-233 item 3). Actor was HR. D-228 item 1 adds: this holds for the creator too.
Given a user whose assignment to project A was revoked
When the Owner assigns them to project A again
Then a new active row is created, and the revoked row is untouched

**AC-114-D — twice is refused**
> Holds as written.
Given an already-revoked assignment
When it is revoked again
Then it is refused with `errors.identity.assignment_already_revoked`

**AC-114-E — nobody else can** *(fails if the rule is broken)*
> Changed 2026-11-18 (D-233 item 3). HR joins the refused list.
Given I am HR, then Finance, then Technical Office, then a Supervisor Site Engineer on that project
When each attempts to remove an assignment
Then each is refused with 403

**AC-114-F — revocation is not deletion** *(fails if the rule is broken)*
Given a revoked assignment
When a delete is attempted against it through the API
Then there is no endpoint that performs one
> Holds as written.

**AC-114-G — Finance cannot be removed** *(fails if the rule is broken)* — added 2026-11-18 (D-228 item 1)
Given an active Finance user on project A, put there automatically
When the Owner attempts to remove that row
Then it is refused with `errors.identity.finance_cannot_be_removed` and the row stays active
And the team read for project A marks that row `canRemove: false`

**AC-114-H — the creator can be removed** — added 2026-11-18 (D-228 item 1)
Given the Technical Office user who created project A, on its team
When the Owner removes him
Then the row is closed (`RevokedAt` set), he gets 403 on project A from his next request, and the team read no longer lists him

**AC-114-I — remove asks first, and cancel changes nothing** — added 2026-11-18 (D-229 item 1)
Given the Owner on the team tab at 390px in Arabic
When he presses «إزالة» on a Site Engineer's row
Then a confirmation «إزالة {name} من المشروع؟» opens with «إزالة» and «إلغاء», and no assignment has changed
And when he presses «إلغاء» the row is still active; and when he presses «إزالة» the row leaves the list and a notice «أُزيل {name}» shows

**AC-114-J — past work stays under his name** — added 2026-11-18 (D-228 item 1)
Given a Site Engineer who recorded a daily-log entry on project A and was then removed
When the Owner reads the entry and the audit trail for project A
Then the entry is still attributed to him by name, and the trail shows his assignment and its removal
And the team read does not list him

**AC-114-K — nobody but the Owner sees the remove control** *(fails if the rule is broken)* — added 2026-11-18 (D-233 item 3)
Given a Technical Office user and a Supervisor Site Engineer assigned to project A
When each reads A's team
Then `canManage` is false and every row's `canRemove` is false, and the screen shows no «إزالة» button

## Not in this story
What deactivating a user does to assignments — **answered**: it revokes them all, through this same
`Revoke`, under the Owner's authority (D-049 ruling 5, KAFF-111). The team tab itself (KAFF-115).

> Changed 2026-11-18. **Design-phase conversion** (who is staffed on the execution project when a design
> project converts, and the Finance gap there, D-228) is not this story's. **The HR team screen** and
> HR removing anyone are out (D-233 item 3). **A per-person history screen** is not built; the audit
> trail (KAFF-117, Owner only) answers "who was on the project and when".

## Readiness waiver — signed, and it does not answer the question
`process/agile.md`'s Definition of Ready says an uncited rule is a question, not a story. **Two rules
here are uncited and are built anyway, under a named waiver** (`decisions.md` D-055 §4, **superseded by D-062 §1 — see below**):

> *"I accept the six stories containing uncited rules to pass them through the Definition of Ready so
> the sprint does not stall. I take this on my own responsibility as the Architect."*

> **✅ COUNTERSIGNED FOR SEVEN — Nabil, 2026-08-22. `decisions.md` D-062 §1.**
>
> *"Signed and approved. I officially approve adding KAFF-106 as the seventh story under the waiver.
> The numerical discrepancy flagged by the Scrum Master is accurate, and the story is essential to
> complete the creation flow for the first user (Owner). This closes the discrepancy and allows the
> build to proceed."*
>
> **Both halves changed.** The count is **seven**, not six — KAFF-100, 101a, 103, 106, 110, 112, 114.
> The signatory is **Nabil**, not the Architect. An Architect's waiver is one agent accepting risk on
> rules Karim has not answered; Nabil is the decision owner's proxy, which is the right weight for
> seven committed stories to be built on.
>
> **It still answers nothing.** Q45–Q51 stay open. The waiver permits the build; the questions remain
> Karim's.
> — **the Architect, signed, 2026-08-22**

| Waived rule | Open question |
|---|---|
| **Rule 7** — the last engineer may be taken off a project, leaving it unstaffed. Source column reads *"§9 — absence noted deliberately"*, which is honest: the silence is real and the conservative reading is the right one, because inventing a minimum team size would be worse. **It is still a rule read out of a silence** | **Q49**, open, for Karim |
| **Rule 4** — revoking an already-revoked assignment is **refused**, not quietly accepted. Sourced to slice-0 code and to nothing Karim said. One of four refusals of the same shape asked as one question | **Q51**, open, for Karim |

**The waiver lets the story be built. It answers neither question**, and both stay open in
`stories/questions-for-karim.md` — Q51 alongside its three siblings (KAFF-110 rule 5, KAFF-112 rule
7, KAFF-106 rule 11).

## Questions for Karim
None that block. Two rules here are sourced to a silence or to slice-0 code rather than
to a ruling, and both are now on the register:
- **Q49** — rule 7 allows revoking the **last** engineer off a project, leaving it unstaffed. Its
  source column reads *"§9 — absence noted deliberately"*, which is honest: the silence is real and
  the conservative reading is the right one, because inventing a minimum team size would be worse.
  It is still a rule read out of a silence. **Does not block.**
- **Q51** — rule 4 refuses revoking an already-revoked assignment, sourced to slice-0 code. Asked with
  three siblings of the same shape (KAFF-110 rule 5, KAFF-112 rule 7, KAFF-106 rule 11).

## Questions for Nabil
Added 2026-11-18. Recommended answers are mine, not rulings.
1. **Q-114-1 — may the Owner remove himself,** if an assignment row of his exists? Recommended: yes, he keeps global reach.
2. **Q-114-2 — what becomes of work a removed person left unfinished** (a draft he started, an open day-labour engagement)? Rule 11 says only that past work stays under his name. Recommended: it stays as it is and the Owner or the next engineer corrects it by new entries, never by edit.
3. **Q-114-3 — removing the only Supervisor.** Rule 7 allows it; the overview then shows «لم يُسنَد مهندس الموقع بعد» (D-229 item 1). Recommended: allowed, no extra confirmation.
