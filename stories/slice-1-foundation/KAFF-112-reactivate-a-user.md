# KAFF-112 · Reactivate a user, who comes back with nothing

**Slice:** 1 · **Epic:** Foundation · **Points:** 3 · **Status:** Ready
**Spec:** §9 · **Decisions:** D-044 (ruling 1), **D-049 (rulings 4, 5)**, **D-051 (N5)**
**Depends on:** KAFF-110, KAFF-111

## Story
As the Owner, I bring back someone who left and has come back — as the same person in the record, and
with none of the access they used to have.

A second account is the failure this story prevents. `spec.md` §2 makes one record per person a defect
condition for clients and employees — *"a second copy of any master record is a defect"* — and the
same logic holds for a login: two accounts mean two actors in the trail for one human, and the trail
is what the money is argued from.

## What Karim ruled
> Leavers are deactivated, never deleted, and stay on historical project teams. **A returning employee
> gets a new password and zero project assignments — nothing is restored automatically.**
> — D-049 ruling 5

Both halves of the old story's Q6 are answered by that one sentence, and the second half is the one
with work in it. **Reactivation restores an identity, not an access.** Whoever needs them on a project
puts them there again, deliberately, and that act is a fresh assignment row with a fresh author.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | `UserManage` covers reactivation, Owner only | D-044 ruling 1 |
| 2 | It is the same `User` row and the same id. Every audit record that named them still names them | §2 (*one record per person*) · CLAUDE.md audit |
| 3 | **The old password does not come back.** A returning employee gets a new one, which means the stored credential is cleared as part of reactivation. **The method now exists: `User.ClearPassword`** — it nulls `PasswordHash`, leaves `MustChangePassword` false because there is no credential left to force a change of, and rotates the stamp [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `ClearPassword`]. ⚠️ **UNCITED — WAIVED, Q50. See "Readiness waiver" below** | D-049 ruling 5 |
| 4 | The new credential arrives the same way a new starter's does: a temporary password the Owner sets, which the user MUST change on first sign-in — `User.SetTemporaryPassword`, which sets `MustChangePassword` [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `SetTemporaryPassword`]. ⚠️ **UNCITED — WAIVED, Q50** | D-049 ruling 4 |
| 5 | **Zero project assignments.** Reactivation restores none of the rows KAFF-111 revoked, and does not create new ones | D-049 ruling 5 |
| 6 | The revoked rows stay revoked and stay on file — they are the historical team membership ruling 5 preserves | D-049 ruling 5 · KAFF-111 |
| 7 | Reactivating an active user is refused, not silently accepted — `errors.identity.user_already_active` [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `IdentityErrors.UserAlreadyActive`]. ⚠️ **UNCITED — WAIVED, Q51. See "Readiness waiver" below** | slice 0 `User.Reactivate` |
| 8 | Reactivation clears `DeactivatedAt` and sets `IsActive` [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `Reactivate`] | slice 0 `User.Reactivate` |
| 9 | The username was never released, so nobody could have created a duplicate account under the old name while they were away | slice 0 unique index on `UserName` |
| 9a | **Reactivation rotates `User.SecurityStamp`.** **`Reactivate` still does not rotate it** [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `Reactivate` — it writes `IsActive` and `DeactivatedAt` and nothing else], while `Deactivate`, `ClearPassword` and the private `StorePasswordHash` behind both password setters all do. **This is still build work.** *(The rule named `SetPasswordHash` until 2026-08-22; that method no longer exists — D-056 §1 split it into `SetOwnPassword` and `SetTemporaryPassword`.)* D-051 (N5) names `Reactivate` as *"the one path that should rotate and does not."* Without it, a token minted before the deactivation and still inside its 30-minute window becomes valid again the moment the account comes back, carrying whatever reach the person had before rule 5 took it away — and the comparison that would let it in **now exists** [Verified: 2026-08-22 @ `src/Infrastructure/Authorization/PermissionSubjectReader.cs` -> `ReadAsync`], so this is no longer a theoretical gap | **D-051 (N5)** · slice 0 `User.Reactivate` · KAFF-101a rule 11a |
| 10 | Role and department come back as they were. Ruling 5 names the password and the assignments; it says nothing about the role, and changing it is KAFF-109's act with KAFF-109's guard | D-049 ruling 5 — read for what it says · §9 |

## Permissions, money, audit, i18n
- **Permissions:** `UserManage`, `CompanyWide`, Owner only.
- **Money:** moves no money. It restores whatever the person's role lets them do with money — which
  is why rule 5 matters: coming back with eight project assignments live would restore reach nobody
  reviewed.
- **Audit:** `Modified` on `User`, `ChangedProperties` naming `IsActive`, `DeactivatedAt` and the
  redacted credential fields, actor = the Owner. A reason is recorded when supplied (Q35, as
  KAFF-110).
- **i18n:** `users.action.reactivate`, `users.confirm.reactivate.title` / **`.body`** — the
  confirmation must say that the person comes back with no projects, because the Owner will otherwise
  assume the opposite — plus `action.confirm`, `action.cancel` and the existing
  `errors.identity.user_already_active`.

  *(Corrected 2026-08-22 under **SM-15**, finding **V-07** / **N-05**. This bullet said
  `users.reactivate`, `users.reactivate.confirm` and `users.reactivate.no_assignments_notice`; S-008
  draws the pair as `users.action.reactivate` and `users.confirm.reactivate.title` / `.body`, and the
  "no projects" sentence **is** `.body` rather than a key of its own
  [Verified: 2026-08-22 @ `ux/slice-1-flows.md` -> `users.confirm.reactivate.title`]. The missing
  `.action.` / `.confirm.` segment is the §6 breach
  [Verified: 2026-08-22 @ `ux/rtl-and-i18n.md` -> `<feature>.action.*`]. Neither spelling is in either
  catalogue yet.)*

## Acceptance criteria
**AC-112-A — a returning user is the same user**
Given a deactivated user with twelve audit records naming them
When the Owner reactivates them
Then it is the same user id, and all twelve records still resolve to it

**AC-112-B — they come back with no access to any project** *(fails if the rule is broken)*
Given a user who was deactivated while assigned to three projects
When the Owner reactivates them and they sign in
Then they hold zero active assignments
And a request against any of those three projects is refused with `errors.auth.not_assigned_to_project`

**AC-112-C — the revoked rows are not resurrected** *(fails if the rule is broken)*
Given the same three assignments
When the assignment table is read after the reactivation
Then all three are still revoked, with their original `RevokedAt` — reactivation did not clear, delete or duplicate them

**AC-112-D — the old password is dead** *(fails if the rule is broken)*
Given a user reactivated after deactivation
When they attempt to sign in with the password they used before they left
Then it is refused, and the refusal is indistinguishable from any other wrong password

**AC-112-E — an old token does not come back to life with them** *(fails if the rule is broken)*
Given a session token minted before the deactivation, still inside its inactivity window
When the Owner reactivates the user and that token is replayed
Then it is refused — reactivation rotated the security stamp (D-051 N5), and KAFF-101a rule 11a compares it

**AC-112-F — the new one is temporary, like a new starter's**
Given the Owner reactivates a user and sets a temporary password
When that user signs in
Then they can reach only the change-password endpoint until they change it (AC-103-B)

**AC-112-G — reactivating an active user is refused**
Given an active user
When the Owner reactivates them
Then it is refused with `errors.identity.user_already_active`

**AC-112-H — only the Owner may**
Given I am HR, then Finance
When each attempts a reactivation
Then each is refused with 403

**AC-112-I — putting them back on a project is a deliberate act with a named author**
Given a reactivated user
When HR assigns them to one of their old projects
Then a **new** assignment row is created, with today's `AssignedAt` and HR as `AssignedByUserId`

## Not in this story
Deactivation (KAFF-110) and the revocation it performs (KAFF-111). Creating a fresh account for a
returning employee — which is the outcome this story exists to make unnecessary. Whether the
temporary password expires if the returner never signs in: **Q37**, which does not block (KAFF-103).

## Readiness waiver — signed, and it does not answer the question
`process/agile.md`'s Definition of Ready says an uncited rule is a question, not a story. **Three
rules here are uncited and are built anyway, under a named waiver** (`decisions.md` D-055 §4, **superseded by D-062 §1 — see below**):

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
| **Rules 3 and 4** — the credential is cleared *and* a temporary one is set, in that order. **D-049 ruling 5 says only *"a new password"***, so the ordering is the story's reading, not Karim's | **Q50**, open, for Karim |
| **Rule 7** — reactivating an already-active account is **refused**, not quietly accepted. Sourced to slice-0 code and to nothing Karim said. One of four refusals of the same shape asked as one question | **Q51**, open, for Karim |

**The waiver lets the story be built. It answers neither question**, and both stay open in
`stories/questions-for-karim.md`.

## Questions for Karim
None that block. **Q37** (temporary-password expiry) touches rule 4 and would add a rule rather than
change one.

- **Q50** — rule 3 says the stored credential is *"cleared as part of reactivation"* and rule 4 has a
  temporary password arrive afterwards. **D-049 ruling 5 says only *"a new password"***, so the two
  rules need one answer: does the returning employee arrive with no credential at all, or is the new
  one set in the same request? **Does not block.**

  > **Half of this question's premise expired on 2026-08-22 and the other half did not.** It read
  > *""cleared" has no method: `User.SetPasswordHash` refuses null or whitespace
  > (`src/Domain/Identity/User.cs`, at lines 160-163 as it then stood)"*. **That method no longer exists** and **the mechanism
  > now does**: `User.ClearPassword`
  > [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `ClearPassword`]. **So the question is no longer
  > "does rule 3 need a domain method that does not exist" — it is purely the workflow question**,
  > and the entity says so itself: *"Nothing calls this yet, deliberately … whether a returning
  > employee arrives with no password or is given one at the moment of reactivation is Q50, open, for
  > Karim — so `Reactivate` is left untouched. Building the mechanism is not choosing when it fires"*
  > [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `ClearPassword`]. **Q50 stays open**, and the same
  > correction is owed to its row in `questions-for-karim.md` -> `Q50`, which the BA does not own.
- **Q51** — rule 7 refuses reactivating an already-active user, sourced to slice-0 code. Asked with
  three siblings of the same shape (KAFF-110 rule 5, KAFF-114 rule 4, KAFF-106 rule 11).
