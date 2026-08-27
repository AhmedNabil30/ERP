# KAFF-111 · Deactivating a user revokes their project assignments

**Slice:** 1 · **Epic:** Foundation · **Points:** 3 · **Status:** ACCEPTED 2026-08-26 — verified on its own criteria for the first time and both QA cases pass. Built **inside KAFF-110's handler** (D-074 §2); it has no endpoint or handler folder of its own and must not be given one. `AC-109-K`'s sibling atomicity claim is now demonstrated under a real injected fault, not argued (`verification-2026-08-26.md` §4.1)
**Spec:** §9 · **Decisions:** **D-049 (ruling 5)**
**Depends on:** KAFF-110, KAFF-113

## Story
As Kaff, when somebody leaves, I take them off the teams they are on and keep the record that they
were on them — so the site manager's team panel is the people who are actually there, and the trail
can still answer who could act on the day an extract was approved.

## What Karim ruled, and the live gap it exposes
> Leavers are **deactivated, never deleted**, and **stay on historical project teams**. A returning
> employee gets a new password and **zero project assignments** — nothing is restored automatically.
> — D-049 ruling 5

The previous version of this story laid out two defensible readings and refused to choose. Karim
chose, and his answer produced **real work rather than a rule to write down** — which D-049 records
in as many words:

> `User.Deactivate` does not touch assignment rows, and `Reactivate` does not either — so **today a
> returning employee would come back with every assignment still active**, which is the opposite of
> the ruling.

That is the shape of the fix, and it is why *"stay on historical teams"* and *"come back with zero
assignments"* are one mechanism and not two: **deactivation revokes the active rows, and the revoked
rows are the history.** `ProjectAssignment.Revoke` already stamps `RevokedAt` and `RevokedByUserId`
and keeps the row [Verified: 2026-08-22 @ `src/Domain/Identity/ProjectAssignment.cs` -> `Revoke`].
**Nothing calls it on deactivation** — `User.Deactivate` still touches only `IsActive`,
`DeactivatedAt` and `SecurityStamp`, and `Reactivate` still touches only `IsActive` and
`DeactivatedAt` [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `Deactivate`, `Reactivate`]. This story is what
calls it.

**It is handler work, not entity work** (D-049): `User` cannot reach the assignment rows, and giving
it a way to would put a query inside an entity to satisfy one rule.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | Deactivating a user revokes **every** active `ProjectAssignment` they hold, in the same request | D-049 ruling 5 |
| 2 | Revocation is a soft close. The row stays, with `RevokedAt` and `RevokedByUserId` — that row *is* the historical team membership the ruling preserves | D-049 ruling 5 · slice 0 `ProjectAssignment.Revoke` |
| 3 | `RevokedByUserId` is the Owner who deactivated the user. The trail must not show the revocations as having no author | CLAUDE.md audit |
| 4 | **A revoked row is not an active one**, so anything that reads active assignments stops returning the leaver while the audit trail and any history view still show them. The access policy's assignment lookup already filters `RevokedAt == null`, so the evaluator half needs no new code [Verified: 2026-08-22 @ `src/Infrastructure/Authorization/ProjectAccessPolicy.cs` -> `AssignedAccessAsync`]. In this sprint that reader is the permission evaluator; the **team panel** that makes it visible is **KAFF-115, deferred** — the rule is the same, its surface arrives later | D-049 ruling 5 · KAFF-115 rule 1 (deferred) |
| 5 | One audit record **per assignment**, not one summary record — CLAUDE.md wants what changed, before and after, and one record cannot carry eight | CLAUDE.md audit |
| 6 | All of them share the deactivation's `CorrelationId`, so one act reads as one story | slice 0 `AuditCorrelationMiddleware` · KAFF-118 rule 5 |
| 7 | Access was already refused for an inactive user regardless of assignment rows. **This story is not what makes the leaver safe** — D-048 does. It is what makes the data say what happened | §9 · D-048 |
| 8 | Nothing is restored on reactivation. A returning employee starts with zero assignments and somebody puts them back deliberately | D-049 ruling 5 · KAFF-112 |
| 9 | Deactivating a user with no assignments is not an error, and writes no assignment records | slice 0 `ProjectAssignment` — nothing to revoke |

## Permissions, money, audit, i18n
- **Permissions:** `UserManage` (Owner only) triggers it. **The revocations happen under the Owner's
  authority, not HR's** — worth naming out loud, because revoking an assignment is normally
  `ProjectAssignmentManage` and HR's daily work. Here the Owner is performing it as part of a
  different act, and the audit records must show that honestly.
- **Money:** moves no money.
- **Audit:** one `Modified` record per `ProjectAssignment`, `ProjectId` set, `ChangedProperties`
  naming `RevokedAt` and `RevokedByUserId`, actor = the Owner, all sharing one `CorrelationId`.
- **i18n:** `users.confirm.deactivate.body` on the confirmation, so the Owner is told the
  consequence in the sentence where he can still stop — S-008 carries it as
  *"They will not be able to sign in, and they will be removed from their projects."*
  [Verified: 2026-08-22 @ `ux/slice-1-flows.md` -> `users.confirm.deactivate.title`].
  *(This said `users.deactivate.revokes_assignments` until 2026-08-22 — a key of its own for a sentence
  that is part of the confirm body, and missing the `.confirm.` segment `ux/rtl-and-i18n.md` §6 fixes.
  Finding **V-07** under **SM-15**; corrected with KAFF-110's matching bullet, noted rather than
  changed silently.)*

## Acceptance criteria
**AC-111-A — the assignments are revoked** *(fails if the rule is broken)*
Given a Site Engineer with active assignments on three projects
When the Owner deactivates them
Then all three rows carry `RevokedAt` and `RevokedByUserId` = the Owner
And `IsActive` is false on all three

**AC-111-B — and the rows survive** *(fails if the rule is broken)*
Given the same three assignments after the deactivation
When the assignment table is read
Then all three rows are still present with `AssignedAt` and `AssignedByUserId` intact — the historical team is not lost

**AC-111-C — the active team loses them, the trail keeps them**
Given a project whose team was two people, one of whom has just been deactivated
When the project's **active** assignment rows are read, and then the audit trail
Then one active row remains, and the trail still shows both and when the second left

*Restated by **SM-10**. It read *"when the team panel is read"* — and **the team panel is KAFF-115,
deferred out of this sprint**, so a committed story carried a criterion nothing in the sprint can
execute. The assertion that matters is about the data, not the panel: the panel is one reader of it,
and KAFF-115 will assert its own rendering when it lands.*

**AC-111-D — one record each, one story** *(fails if the rule is broken)*
Given a deactivation revoking three assignments
When the audit records are read
Then there are four records — one `User`, three `ProjectAssignment` — and all four share one `CorrelationId`

**AC-111-E — a leaver with no assignments deactivates cleanly**
Given a Finance user with no assignment rows
When the Owner deactivates them
Then it succeeds, and exactly one audit record is written

**AC-111-F — nothing comes back on its own** *(fails if the rule is broken)*
Given the deactivated engineer of AC-111-A
When the Owner reactivates them
Then they hold **zero** active assignments, and the three revoked rows are still revoked

**AC-111-G — the whole act is one transaction** *(fails if the rule is broken)*
Given a deactivation in which revoking the third assignment fails
When the database is read afterwards
Then the user is still active and no assignment is revoked — there is no half-deactivated user

## Not in this story
Deactivation itself (KAFF-110) — this is its second half. Reactivation (KAFF-112). Revoking a single
assignment deliberately, which is HR's normal act (KAFF-114).

## Questions for Karim
None. D-049 ruling 5 answers the question this story was written to carry.
