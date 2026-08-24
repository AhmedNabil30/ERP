# KAFF-103 · Change the temporary password on first sign-in

**Slice:** 1 · **Epic:** Foundation · **Points:** 5 · **Status:** Ready
**Spec:** §9 · **Decisions:** **D-049 (rulings 3, 4)**, D-050
**Depends on:** KAFF-100 *(soft)*, KAFF-101a, KAFF-106

## Story
As a newly created user, I am made to replace the password the Owner gave me before I can do
anything else — so that from that moment on, nobody but me knows the credential that acts as me.

## What Karim ruled, and why this story changed shape
The previous version of this story had the new user set their own password through an invitation,
and its AC3 asserted *"the creator never learns the password"*. **Karim ruled the other way**, and
gave the reason that makes it safe:

> Onboarding is **a temporary password set by the Owner, which the user MUST change on first
> sign-in**. Site engineers often have no company email, so a reset link cannot be the primary path.
> Forcing the change is what keeps the audit trail meaningful: **after it, the Owner does not know
> the credential that acts as that user.** — D-049 ruling 4

So the Owner *does* hold a working credential for a short window, deliberately, and the forced change
is what closes it. The separation-of-duties concern the old story raised — §9's *"nobody creates and
approves the same movement"* — is answered by the change being mandatory, not by the Owner never
knowing the password. **The window is the residual risk, and it is Karim's to accept; he has.**

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | The Owner sets a temporary password when creating a user (or afterwards). The user cannot sign in until one exists | D-049 ruling 4 · KAFF-106 |
| 2 | The user MUST change it on first sign-in. Until they do, the session reaches **only** the change-password endpoint | D-049 ruling 4 |
| 3 | The new password is at least 8 characters. **No complexity is demanded** — no symbol rule, no digit rule, no mixed case | D-049 ruling 3 |
| 4 | Changing the password rotates the security stamp, which ends **every** other session for that user, everywhere, immediately. **The method to call is `User.SetOwnPassword` — the holder types this one themselves, so no forced change follows. `SetPasswordHash` no longer exists**, having been split into `SetOwnPassword` and `SetTemporaryPassword`; the rotation is in the private `StorePasswordHash` both reach [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `SetOwnPassword`, `SetTemporaryPassword`, `StorePasswordHash`] | D-049 ruling 2 · `User.SetOwnPassword` |
| 5 | The change endpoint requires the current password as well as the new one. Otherwise an unattended signed-in phone is a password reset. ⚠️ **UNCITED — WAIVED, Q48. See "Readiness waiver" below** | §9 — the same *"the server decides"* reasoning; and rule 2 means the session is by definition one nobody has yet proved they own |
| 6 | `Role.Subcontractor` can never be given a password to change. The refusal is in the private `StorePasswordHash`, so **both** public setters inherit it [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `StorePasswordHash`] | §9 · `User.StorePasswordHash` |
| 7 | The password reaches the server once, is hashed, and is never stored, logged or audited in any recoverable form [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `PasswordHash`, `SecurityStamp` — `[AuditRedacted]` is on both] | CLAUDE.md audit · `[AuditRedacted]` on `PasswordHash` |
| 8 | The system records that the change happened, so it can tell a temporary password from a chosen one. Without that flag rule 2 cannot be enforced. **The flag exists: `User.MustChangePassword`, set by `SetTemporaryPassword` and cleared by `SetOwnPassword`** — do not build it again [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `MustChangePassword`, `SetTemporaryPassword`, `SetOwnPassword`; migration `20260821221842_UserLockoutAndForcedPasswordChange`] | D-049 ruling 4 — the mechanism the ruling requires |
| 9 | Nothing here changes role, department, assignments or `IsActive` | §9 |

## Permissions, money, audit, i18n
- **Permissions:** authenticated as the user themselves. **Not `UserManage`** — the Owner sets the
  temporary credential (KAFF-106); only the person changes it.
- **Money:** moves no money.
- **Audit:** `Modified` on `User`, naming the user as their own actor. The before/after JSON must
  show the hash and the security stamp as **redacted, not absent** — an absent key reads as
  "unchanged" (AC-118-F). The record is what lets the trail say *"from 14:12 on 3 June, only
  this person could have been signing in as this person."*
- **i18n:** `auth.password.title`, `auth.password.must_change`, `auth.field.current_password`,
  `auth.field.new_password`, `auth.field.confirm_password`, `auth.password.rule_min_length`,
  `auth.password.hint.ends_other_sessions`, `action.save`, `auth.password.changed`,
  `errors.auth.password_too_short`, `errors.auth.current_password_incorrect`,
  `errors.auth.password_change_required`. All of them are new and need entries in **both** `ar.json`
  and `en.json`.

  *(Corrected 2026-08-22 under **SM-15**, finding **V-07** / **N-05**. This bullet named
  `auth.change_password.title`, `.current`, `.new`, `.confirm`, `.submit` and `.required_notice` —
  **completely disjoint** from the keys S-003 is drawn against
  [Verified: 2026-08-22 @ `ux/slice-1-flows.md` -> `auth.password.must_change`], and `.submit` and
  `.current` breach `<feature>.field.*` / `action.*` besides
  [Verified: 2026-08-22 @ `ux/rtl-and-i18n.md` -> `<feature>.field.*`]. Six keys, one screen, two
  spellings — F-08's shape. Neither spelling is in either catalogue yet, so the cost is this text
  edit. The three `errors.*` keys are unchanged: they are the server's and Backend owns them
  (`ux/rtl-and-i18n.md` hard rule 1).)*

## Acceptance criteria
**AC-103-A — a new user changes the temporary password and is then free**
Given the Owner created my account with a temporary password
When I sign in and change it
Then I can use the rest of the system, and an audit record of `Modified` names me as the actor

**AC-103-B — until then, nothing else is reachable** *(fails if the rule is broken)* — ⚠️ **one of the
three endpoints it names is contested.** `GET /api/auth/me` is the endpoint the shell needs in order to
know a forced change is required at all; whether it is inside this gate (refused) or outside it
(answers, carrying the flag) is **N-04 / Q-UX-18, action SM-16, open, BA + Architect** — see
`KAFF-105a` -> *"The `password_change_required` shape is undecided"*. **The list endpoint and the write
endpoint are not contested and this criterion is executable against them today.** Do not resolve the
`/api/auth/me` half by building it. Finding **V-03**.
Given I have signed in with a temporary password and not yet changed it
When I call, in turn, `GET /api/auth/me`, a list endpoint and a write endpoint
Then every one except the change-password endpoint is refused with `errors.auth.password_change_required`

**AC-103-C — the Owner's credential stops working the moment I change it** *(fails if the rule is broken)*
Given the Owner knows the temporary password
When I have changed it
Then a sign-in attempt with the temporary password is refused, and is indistinguishable from any other wrong password

**AC-103-D — the current password is required** *(fails if the rule is broken)*
Given a signed-in session
When a password change is submitted without the current password, or with the wrong one
Then it is refused with `errors.auth.current_password_incorrect`, and the stored hash is unchanged

**AC-103-E — eight characters, and nothing more, is the whole rule** *(fails if the rule is broken)*
Given a new password of exactly 8 lower-case letters
When it is submitted
Then it is accepted
And a password of 7 characters is refused with `errors.auth.password_too_short`

**AC-103-F — the change ends every other session** *(fails if the rule is broken)*
Given the same user signed in on two devices
When the password is changed on one
Then the other device is refused on its next request

**AC-103-G — the creator never learns the chosen password**
Given the Owner created my account
When the Owner reads the user record, the API response and the audit trail after my change
Then no field carries my password or its hash, in plain text or in recoverable form

**AC-103-H — a subcontractor record has nothing to change**
Given a `User` with `Role.Subcontractor`
When a password set or change is attempted for it
Then it is refused with `errors.identity.subcontractor_cannot_log_in`

**AC-103-I — Arabic, RTL, at mobile width**
Given the screen at 390px in Arabic
When it renders
Then direction is RTL, no literal strings, no horizontal overflow — engineers will do this on a phone

## Not in this story
Recovering a password you have forgotten (KAFF-104, now `Ready` — an Owner-generated single-use reset
link the user follows to set their **own** password, D-051 Q38). The temporary password the
Owner types — that is part of user creation, KAFF-106. Two-factor anything: not in `spec.md`, not to
be added by an agent.

## An edge the ruling does not cover — raised, not decided
**Does a temporary password expire?** Ruling 4 says the Owner sets one and the user must change it;
it says nothing about how long it stays usable. **This story is built with no expiry**, because that
is exactly what was ruled — adding one would be adding a rule Karim did not give, and the ruling is
buildable as it stands. The consequence, stated so it is visible: an account created and forgotten
about keeps a credential the Owner knows, indefinitely. Raised as **Q37**.

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
| **Rule 5** — the change endpoint demands the **current** password. The source column reads *"§9 — the same reasoning"*, and **§9 says nothing about it.** The reasoning is good and it is still a rule read into a silence — and rule 2 cuts slightly the other way, since a first-sign-in session belongs to somebody who has not yet proved they own it | **Q48**, open, for Karim |

**The waiver lets the story be built. It does not answer Q48**, which stays open in
`stories/questions-for-karim.md`. The story is buildable either way.

## Questions for Karim
- **Q37** — should the temporary password stop working after a while if the person never signs in?
  **Does not block this story**; the answer would add a rule rather than change the one given.
- **Q48** — must the change-password endpoint demand the **current** password? Rule 5 says yes and
  cites *"§9 — the same reasoning"*; **§9 says nothing about it.** The reasoning is good — an
  unattended signed-in phone would otherwise be a password reset — and it is still a rule read into a
  silence. **Does not block**; the story is buildable either way, and rule 2 cuts slightly the other
  way, since a first-sign-in session belongs to somebody who has not yet proved they own it.
