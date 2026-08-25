# KAFF-110 · Deactivate a user, and their access ends on the next request

**Slice:** 1 · **Epic:** Foundation · **Points:** 5 · **Status:** BUILT, verified 2026-08-25 — 8 of 10 satisfied, `AC-110-E` deferred to KAFF-104, `AC-110-D` deferred to KAFF-101a. **The deferral marker was already in this file** — the Verifier's finding W-9 was stale when written, not unactioned; the Scrum Master's ruling of 2026-08-25 confirms it rather than creating it. W-9 is closed; awaiting Nabil's acceptance
**Spec:** §9 · **Decisions:** D-044 (ruling 1), D-048, **D-049 (rulings 2, 5)**
**Depends on:** KAFF-106

## Story
As the Owner, I deactivate someone who has left Kaff, and their access ends immediately — not when
their session happens to expire, and not only on the device they are holding.

The distinction is load-bearing and has been found wrong twice already. The kickoff recorded that
*"the Owner branch skipped the active-user check the class documented, so a deactivated Owner kept
unrestricted reach until token expiry"*. Then QA's F-11 found the same hole from the other side: the
access policy was consulted **only when the request named a project**, so every company-wide
permission was decided from token claims alone — *"a deactivated Owner kept `UserManage`"*. D-048
closed it: the token supplies identity, the database supplies authority, on every request.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | `UserManage` covers deactivation, Owner only | D-044 ruling 1 |
| 2 | A deactivated user is refused on the **next request**, with a session that was valid a second earlier, on **every** device | §9 · D-048 · D-049 ruling 2 |
| 3 | Deactivation rotates the security stamp, which is what ends the sessions the user is not holding [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `Deactivate`]. **The comparison that makes the rotation bite now exists** — the subject read refuses a stamp mismatch, and an absent stamp, in the `WHERE` clause [Verified: 2026-08-22 @ `src/Infrastructure/Authorization/PermissionSubjectReader.cs` -> `ReadAsync`; D-053 §1]. It did not when this rule was written | slice 0 `User.Deactivate` · **D-053 §1** · D-049 ruling 2 |
| 4 | It applies to the Owner too. There is no account the rule exempts — the subject read filters on `user.IsActive` before any role is considered, so a deactivated Owner produces no subject at all [Verified: 2026-08-22 @ `src/Infrastructure/Authorization/PermissionSubjectReader.cs` -> `ReadAsync`] | §9 · D-048 |
| 5 | Deactivating twice is refused rather than silently accepted — `errors.identity.user_already_inactive` [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `IdentityErrors.UserAlreadyInactive`]. ⚠️ **UNCITED — WAIVED, Q51. See "Readiness waiver" below** | slice 0 `User.Deactivate` |
| 6 | A user is **never deleted.** *"Leavers are deactivated, never deleted"* — and the audit trail names actors by id, so a deleted user makes every record they wrote unreadable | D-049 ruling 5 · CLAUDE.md audit |
| 7 | **Deactivation revokes every active project assignment**, and the revoked rows stay on file. That is KAFF-111, and it is the same act — one request, one correlation id | D-049 ruling 5 · KAFF-111 |
| 8 | The person stays on **historical** project teams. Revoked rows are history and are exactly what ruling 5 asks be kept — `Revoke` stamps `RevokedAt` and `RevokedByUserId` and keeps the row, and `IsActive` is computed from `RevokedAt` rather than stored [Verified: 2026-08-22 @ `src/Domain/Identity/ProjectAssignment.cs` -> `Revoke`, `RevokedAt`, `IsActive`] | D-049 ruling 5 · slice 0 `ProjectAssignment` |
| 9 | Deactivation changes nothing about the work the person did. Nothing they created is withdrawn, reversed or hidden | CLAUDE.md (*append-only*) · §7 |
| 10 | A deactivated user cannot sign in and cannot recover a password back in | KAFF-101a · KAFF-104 |
| 11 | **Unresolved:** whether the Owner must type a reason, or may leave it blank | **Q35** — does not block; see below |

## Permissions, money, audit, i18n
- **Permissions:** `UserManage`, `CompanyWide`, Owner only.
- **Money:** moves no money. It removes someone's ability to move money, which must be observable in
  the trail at a timestamp.
- **Audit:** `Modified` on `User` with `IsActive` and `DeactivatedAt` in `ChangedProperties`, actor =
  the Owner. **A reason is recorded when it is supplied**, verbatim — `IAuditContext` already carries
  one, and CLAUDE.md asks for a reason *"where the flow requires it"*. Whether this flow *requires*
  one is Q35 and is not asserted here. Plus **one record per revoked assignment** (KAFF-111): a
  single summary record cannot carry eight.
- **i18n:** `users.action.deactivate`, `users.confirm.deactivate.title` / `.body`,
  `users.danger.title`, `users.danger.deactivate_explains`, `users.field.deactivation_reason`,
  `users.state.inactive`, `action.confirm`, `action.cancel`, and the existing
  `errors.identity.user_already_inactive`.

  *(Corrected 2026-08-22 under **SM-15**, finding **V-07** / **N-05**. This bullet said
  `users.deactivate`, `users.deactivate.confirm`, `users.deactivate.reason`,
  `users.deactivate.revokes_assignments` and `users.status.active` / `.inactive` — all of them missing
  the `.action.` / `.confirm.` / `.field.` segment `ux/rtl-and-i18n.md` §6 fixes
  [Verified: 2026-08-22 @ `ux/rtl-and-i18n.md` -> `<feature>.confirm.*`], and S-008 draws them as
  above [Verified: 2026-08-22 @ `ux/slice-1-flows.md` -> `users.danger.deactivate_explains`]. The
  "removed from their projects" sentence is not a key of its own — it is
  `users.confirm.deactivate.body`. `users.state.inactive` is the list chip S-006 already names
  [Verified: 2026-08-22 @ `ux/slice-1-flows.md` -> `users.state.inactive`]. Neither spelling is in
  either catalogue yet. **The reason field stays keyed but stays optional** — the mandatory-reason rule
  was withdrawn to **Q35** and nothing here reinstates it.)*

## Acceptance criteria
**AC-110-A — access ends on the next request** *(fails if the rule is broken)*
Given a Finance user holding a valid session, who has just succeeded on a request
When the Owner deactivates them
And they repeat the identical request with the same session
Then it is refused, and no state was changed by the attempt

**AC-110-B — including on the requests that name no project** *(fails if the rule is broken)*
Given a deactivated Owner holding a session issued before the deactivation
When they call a `CompanyWide` endpoint requiring `UserManage`
Then it is refused — this is the F-11 path, and it must be exercised separately from the project-scoped one

**AC-110-C — every device, not just this one** *(fails if the rule is broken)*
Given a user signed in on two devices
When the Owner deactivates them
Then both devices are refused on their next request

**AC-110-D — they cannot sign in again** *(moves with **KAFF-101a**, deferred)*
Given a deactivated user
When they attempt to sign in with their correct password
Then it is refused, and the refusal does not reveal that the account was deactivated rather than never existing

**AC-110-E — and cannot recover their way back in** *(moves with **KAFF-104**, deferred)*
Given a deactivated user
When they attempt password recovery
Then it is refused, and the refusal does not reveal that the account was deactivated rather than never existing

*Split by **SM-10**. AC-110-D exercised recovery in the same breath as sign-in, and **password recovery is
KAFF-104, deferred out of this sprint** — so a committed story's criterion could only half execute.
Rule 10 still says both, and the recovery half is asserted where it can be run. Note the refusal
KAFF-104 must give is the *same* refusal, for the same reason: an enumerable recovery endpoint leaks
exactly what an enumerable sign-in leaks.*

**AC-110-F — their assignments are revoked, and stay on file** *(fails if the rule is broken)*
Given a Site Engineer with active assignments on three projects
When the Owner deactivates them
Then all three rows carry `RevokedAt` and `RevokedByUserId`
And all three rows still exist, so the historical team is intact
And three audit records exist, one per assignment, sharing the deactivation's `CorrelationId`

**AC-110-G — the reason is stored when it is given**
Given the Owner deactivates a user and supplies a reason
When the audit record is read
Then the reason is stored verbatim on it

**AC-110-H — the record survives**
Given a deactivated user who created twelve audit records
When those records are read
Then all twelve still name them, and the user row still exists

**AC-110-I — only the Owner may**
Given I am HR, then Finance
When each attempts to deactivate a user
Then each is refused with 403

**AC-110-J — twice is refused**
Given an already-inactive user
When the Owner deactivates them again
Then it is refused with `errors.identity.user_already_inactive`, and no assignment is touched a second time

## Not in this story
The assignment revocation itself is specified in **KAFF-111** — it is one act, split across two
stories because the revocation is where the work is. Reactivation (KAFF-112). Deleting a user: out of
scope permanently, see rule 6.

## The mandatory-reason rule was withdrawn, deliberately
The previous version carried an **earlier** AC4 (not AC-110-D above, which is about signing in):
*"deactivation requires a reason … given no reason supplied, it is
refused."* QA is right that no cited source states it (`qa/questions.md` QA-3): `User.Deactivate`
takes only a timestamp, and `IAuditContext.SetReason` is a voluntary call. CLAUDE.md's *"why where
the flow requires it"* is the judgement, not the rule. **The requirement has been withdrawn to a
question (Q35) and the story is Ready without it** — recording the reason when it is supplied is
cited and is built; refusing without one is not, and would be this backlog's own failure mode.

## Readiness waiver — signed, and it does not answer the question
`process/agile.md`'s Definition of Ready says an uncited rule is a question, not a story. **Rule 5 is
uncited and is built anyway, under a named waiver** (`decisions.md` D-055 §4, **superseded by D-062 §1 — see below**):

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
| **Rule 5** — deactivating an already-inactive account is **refused**, not quietly accepted. Sourced to slice-0 code [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `IdentityErrors.UserAlreadyInactive`] and to **nothing Karim said**. One of four refusals of the same shape asked as one question | **Q51**, open, for Karim |

**The waiver lets the story be built. It does not answer Q51**, which stays open in
`stories/questions-for-karim.md` alongside its three siblings (KAFF-112 rule 7, KAFF-114 rule 4,
KAFF-106 rule 11). **Note this is separate from the mandatory-reason rule below**, which was not
waived — it was *withdrawn* to Q35, which is the other correct outcome and is not the same thing.

## Questions for Karim
- **Q35** — *"When you switch someone's account off, should the system make you type why, or is that
  optional?"* *(Merged from `qa/questions.md` QA-3.)* **Does not block.** If the answer is yes, the
  same shape applies to every rejection gate in slice 5 and the mechanism should be built once.
- **Q51** — rule 5 refuses a second deactivation of an already-inactive account, sourced to slice-0
  code and to nothing Karim said. Asked with three siblings of the same shape (KAFF-112 rule 7,
  KAFF-114 rule 4, KAFF-106 rule 11). **Does not block** — it is built that way and probably right.
