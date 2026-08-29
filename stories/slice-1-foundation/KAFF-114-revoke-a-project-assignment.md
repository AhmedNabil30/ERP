# KAFF-114 · Revoke a project assignment without losing who could act when

**Slice:** 1 · **Epic:** Foundation · **Points:** 3 · **Status:** ACCEPTED 2026-08-26, standing. No commit since has touched this story's own code. `TC-1-120` is still uncovered — QA → Backend, P2
**Spec:** §9, §7 · **Decisions:** D-044 (ruling 3)
**Depends on:** KAFF-113

## Story
As the Owner or HR, I take someone off a project, and the record of them having been on it survives —
so that six months later the trail can still answer who was allowed to act on the day an extract was
approved.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | `ProjectAssignmentManage`, Owner and HR, both with global reach | §9 · D-012 · D-044 rulings 1, 3 |
| 2 | Revocation is a soft close: `RevokedAt` and `RevokedByUserId` are stamped and the row stays. It is never a delete. `IsActive` is the computed `RevokedAt is null`, so there is no stored flag that can drift from the timestamp [Verified: 2026-08-22 @ `src/Domain/Identity/ProjectAssignment.cs` -> `Revoke`, `RevokedAt`, `IsActive`] | slice 0 `ProjectAssignment` |
| 3 | Access ends on the next request. The assignment is read per request, not baked into the token — the access policy queries `RevokedAt == null` on every project-scoped decision [Verified: 2026-08-22 @ `src/Infrastructure/Authorization/ProjectAccessPolicy.cs` -> `AssignedAccessAsync`] | §9 (*"Enforcement is server-side"*) |
| 4 | Revoking an already-revoked assignment is refused — `errors.identity.assignment_already_revoked` [Verified: 2026-08-22 @ `src/Domain/Identity/ProjectAssignment.cs` -> `Revoke`; @ `src/Domain/Identity/IdentityErrors.cs` -> `AssignmentAlreadyRevoked`]. ⚠️ **UNCITED — WAIVED, Q51. See "Readiness waiver" below** | slice 0 `ProjectAssignment.Revoke` |
| 5 | Revocation withdraws nothing the person did. Anything they created stands, and corrections are new movements, never edits | CLAUDE.md (*append-only*) · §7 |
| 6 | After revocation the same user may be assigned again — the unique index covers active rows only | slice 0 `ProjectAssignment` |
| 7 | Revoking the last engineer on a project is allowed. Nothing in `spec.md` requires a project to have anyone on it, and inventing a minimum would be a rule nobody asked for. ⚠️ **UNCITED — WAIVED, Q49. See "Readiness waiver" below** | §9 — absence noted deliberately |

## Permissions, money, audit, i18n
- **Permissions:** `ProjectAssignmentManage`, `ProjectScoped`, Owner and HR.
- **Money:** moves no money.
- **Audit:** `Modified` on `ProjectAssignment`, `ProjectId` set, `ChangedProperties` naming
  `RevokedAt` and `RevokedByUserId`, actor = Owner or HR.
- **i18n:** `assignments.action.revoke`, `assignments.confirm.revoke.title` / `.body`,
  `a11y.revoke_assignment`, `assignments.revoked_on`, `action.confirm`, `action.cancel`, and the
  existing `errors.identity.assignment_already_revoked`.

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
Given a Site Engineer assigned to project A, holding a valid token, who has just written a daily-log-style request successfully
When HR revokes the assignment
And the engineer repeats the identical request with the same token
Then it is refused with 403 and `errors.auth.forbidden`

**AC-114-B — the row survives** *(fails if the rule is broken)*
Given a revoked assignment
When the assignment table is read
Then the row is still there with `AssignedAt`, `AssignedByUserId`, `RevokedAt` and `RevokedByUserId` all populated

**AC-114-C — re-assignment is legal**
Given a user whose assignment to project A was revoked
When HR assigns them to project A again
Then a new active row is created, and the revoked row is untouched

**AC-114-D — twice is refused**
Given an already-revoked assignment
When it is revoked again
Then it is refused with `errors.identity.assignment_already_revoked`

**AC-114-E — nobody else can** *(fails if the rule is broken)*
Given I am Finance, then Technical Office, then a Supervisor Site Engineer on that project
When each attempts to revoke an assignment
Then each is refused with 403

**AC-114-F — revocation is not deletion** *(fails if the rule is broken)*
Given a revoked assignment
When a delete is attempted against it through the API
Then there is no endpoint that performs one

## Not in this story
What deactivating a user does to assignments — **answered**: it revokes them all, through this same
`Revoke`, under the Owner's authority rather than HR's (D-049 ruling 5, KAFF-111). The team panel
(KAFF-115).

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
