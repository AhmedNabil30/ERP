# KAFF-104 · Reset a forgotten password with an Owner-generated link

**Slice:** 1 · **Epic:** Foundation · **Points:** 5 · **Status:** Ready
**Spec:** §9 · **Decisions:** D-044 (ruling 1), D-049 (rulings 2, 3, 4, 5), **D-051 (Q38)**, **D-051 (N5)**
**Depends on:** KAFF-101a, KAFF-103, KAFF-106

## Story
As a user who has forgotten my password, I ring the office; the Owner generates a reset link that
goes to my registered phone; I follow it and **set my own password** — so that recovering access
never leaves anybody else holding a credential that acts as me.

Password recovery is the classic back door into a system that has otherwise been careful. This one
guards the Owner account too, and the Owner approves every financial movement in Kaff (§7, §9).

## What Karim ruled
> **The employee tells the office; the Owner generates a temporary reset link; it goes to their
> registered phone by SMS or WhatsApp.** The Owner must **not** type a new password: *"that would
> compromise the non-repudiation of the Audit Trail."* — **D-051 (Q38)**

**This is the same reasoning as D-049 ruling 4, applied consistently** (D-051 says so in as many
words): if the Owner sets a password the user keeps, every action that account takes has two possible
authors. Onboarding accepts a brief window because the forced first change closes it; recovery does
not need even that window, so it does not get one.

**It also settles the rule the previous version of this story could not decide.** The old rule —
*"`UserManage` must not be a route to reset someone else's password"* — **stands**, and now has a
citation. The Owner triggers the reset; the Owner never learns the credential.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | The Owner triggers the reset. It is `UserManage`, Owner only — the same permission that creates users | **D-051 (Q38)** · D-044 ruling 1 |
| 2 | **The Owner cannot set the password.** There is no field for it on this endpoint and no code path that reaches a password setter with an Owner-supplied value. **The setters are `User.SetOwnPassword` and `User.SetTemporaryPassword`; `SetPasswordHash` no longer exists** [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `SetOwnPassword`, `SetTemporaryPassword`]. The complete endpoint calls **`SetOwnPassword`** — the user typed it, so no forced change follows (rule 9) | **D-051 (Q38)** |
| 3 | What the Owner gets back is a **link**, which goes to the user's **registered phone** by SMS or WhatsApp | **D-051 (Q38)** |
| 4 | The user follows the link and **types their own password**, at least 8 characters with no forced complexity | **D-051 (Q38)** · D-049 ruling 3 |
| 5 | **The link is single-use.** It is consumed the moment a password is set through it, and a second use is refused. A link that keeps working is a standing credential for whoever still has the message — which is exactly the non-repudiation failure ruling 38 exists to prevent | **D-051 (Q38)**, its stated reason |
| 6 | **The link expires.** The lifetime is a configured value (`JwtOptions`-style, **never a literal in a handler** — the precedent is KAFF-101a rule 5). The story requires that a finite lifetime exists and is enforced server-side; **the number itself is N8**, an Architect decision, and it does not change the shape of anything here | D-049 ruling 2's shape · **N8** |
| 7 | **Completing a reset kills every active session, everywhere, immediately.** A reset **is** a password change, and ruling 2 says a password change invalidates every session. Mechanically this is `User.SetOwnPassword` rotating `User.SecurityStamp` through the private `StorePasswordHash` [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `SetOwnPassword`, `StorePasswordHash`], which KAFF-101a validates per request [Verified: 2026-08-22 @ `src/Infrastructure/Authorization/PermissionSubjectReader.cs` -> `ReadAsync`] | D-049 ruling 2 · **D-051 (N5)** · D-053 §1 |
| 8 | Generating a link **invalidates any link generated earlier** for that user. Two live links is two live credentials | rule 5, same reasoning |
| 9 | Because the user chooses the password themselves, there is **no forced change on first use** afterwards — nobody else knows it. This is the same reading as KAFF-100 rule 8 and D-049 ruling 4 read for what it says. **The mechanism is `SetOwnPassword` leaving `MustChangePassword` false; calling `SetTemporaryPassword` here would be the defect** [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `SetOwnPassword`, `SetTemporaryPassword`] | D-049 ruling 4, read for what it says · **D-051 (Q38)** |
| 10 | An inactive user cannot be reset. Recovery must not be a route back in for somebody who has left; a returning employee comes back through reactivation with a temporary password and **zero** assignments | §9 · D-049 ruling 5 · KAFF-112 |
| 11 | `Role.Subcontractor` has no password to reset. The refusal is in the private `StorePasswordHash`, inherited by both public setters [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `StorePasswordHash`] | §9 · `User.StorePasswordHash` |
| 12 | A user with no phone on file cannot be reset, and the refusal says so — the link has nowhere to go | **D-051 (Q38)** · KAFF-100 rule 11 |
| 13 | A reset never changes role, department, assignments or `IsActive` | §9 |
| 14 | **A reset does not release a lockout.** Five failed attempts lock the account for 15 minutes and recovery is not a way around it. Note what the entity does and does not do: `LockedOutUntil` is cleared **only** by `RecordSuccessfulSignIn`, and no password setter touches it [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `LockedOutUntil`, `RecordSuccessfulSignIn`, `RecordFailedSignIn`, `StorePasswordHash`] — so this rule holds by construction today, and the criterion below is what keeps it holding | D-049 ruling 3 |
| 15 | The link token is stored hashed, never in clear, and never appears in a log or an audit record — it is a credential for the length of its life | CLAUDE.md audit · slice 0 `[AuditRedacted]` precedent |

## Permissions, money, audit, i18n
- **Permissions:** **two endpoints, two different answers.**
  - *Generate* — `UserManage`, `CompanyWide`, **Owner only**. Not HR: HR manages employee records
    (D-044 ruling 2) and holds no `UserManage`.
  - *Complete* — **anonymous**, bound to a single-use token. It is the third and last anonymous
    endpoint in the system (with setup, KAFF-100, and sign-in, KAFF-101a) and, like both of those,
    its correctness rests entirely on one check.
- **Money:** moves no money. It restores access to an account that may approve every financial
  movement in Kaff, which is why it is a 5.
- **Audit:** **two records, and both matter.** The generation (actor = the Owner, subject = the user,
  **no token value**) and the completion (actor = the user). A burst of generations against one
  username is a signature; only the pair shows whether it succeeded. An **expired or reused** link
  presented to the complete endpoint writes a record too, with no actor.
- **i18n:** `users.reset_password`, `users.reset_password.confirm`,
  `users.reset_password.link_generated`, `auth.reset.title`, `auth.reset.submit`,
  `errors.auth.reset_link_invalid` (covering expired, consumed and unknown — one key, for the same
  reason KAFF-101a returns one for three failures), `errors.identity.user_has_no_phone`, and the
  reused `errors.auth.password_too_short`. Both catalogues.

## Acceptance criteria
**AC-104-A — the Owner never holds the credential** *(fails if the rule is broken)*
Given the Owner generates a reset for a Finance user
When the request and response are inspected, and the codebase searched
Then no field on this endpoint accepts a password, and no path sets one from Owner-supplied input
And the user's stored password hash is unchanged until **they** complete the reset

**AC-104-B — the link works once** *(fails if the rule is broken)*
Given a generated reset link
When the user follows it and sets a password of 8 lower-case letters
Then it succeeds, and they can sign in with it
And following the same link a second time is refused with `errors.auth.reset_link_invalid`

**AC-104-C — the link expires** *(fails if the rule is broken)*
Given a generated reset link and the configured lifetime elapsed
When it is followed
Then it is refused with `errors.auth.reset_link_invalid`
And the configured lifetime is read from options — a search of handlers finds no literal duration

**AC-104-D — a second link kills the first** *(fails if the rule is broken)*
Given the Owner generates a link, then generates a second for the same user
When the first link is followed
Then it is refused

**AC-104-E — every session dies** *(fails if the rule is broken)*
Given a user holding live sessions on two devices
When they complete a reset
Then both devices are refused on their next request — the security stamp rotated and KAFF-101a validates it

**AC-104-F — the user is not asked to change it again**
Given a completed reset
When the user signs in with the password they just chose
Then they reach the application and are not routed to the change-password screen

**AC-104-G — a deactivated user cannot be reset** *(fails if the rule is broken)*
Given a user whose `IsActive` is false
When the Owner attempts to generate a reset
Then no link is generated, and reactivation (KAFF-112) is the only way back

**AC-104-H — no phone, no reset**
Given a user with no phone on file
When the Owner attempts to generate a reset
Then it is refused with `errors.identity.user_has_no_phone`

**AC-104-I — a reset does not shortcut a lockout** *(fails if the rule is broken)*
Given an account locked by five failed attempts
When a reset is completed within the fifteen minutes
Then the account is still locked for the remainder — a reset is not a lock release

**AC-104-J — a reset changes nothing but the credential**
Given a user with role Finance, department Finance and two project assignments
When they complete a reset
Then role, department and both assignments are unchanged

**AC-104-K — a subcontractor has nothing to reset**
Given a `User` with `Role.Subcontractor`
When the Owner attempts to generate a reset
Then it is refused

**AC-104-L — only the Owner may generate**
Given I am HR, then Finance, then the user themselves
When each attempts to generate a reset for somebody
Then each is refused with 403

**AC-104-M — the token never appears anywhere it can be read later** *(fails if the rule is broken)*
Given a generated and then completed reset
When the application log, both audit records and the database row are inspected
Then none contains the token in clear

**AC-104-N — both ends are audited**
Given a reset generated at 10:00 and completed at 10:04
When the trail is read
Then it holds one record naming the Owner as actor and one naming the user, four minutes apart

## Not in this story
Changing a password you still know (KAFF-103). Onboarding a new starter, which stays a temporary
password the Owner sets (KAFF-103, D-049 ruling 4). Reactivating a leaver, which issues a fresh
temporary password by ruling 5 and is KAFF-112. Two-factor anything.

**And the delivery itself — see below.** This story generates the link and enforces every rule about
it. Putting the message on a wire is not in the stack (`CLAUDE.md`) and is **N7**.

## The delivery channel — **N7**, for Nabil and the Architect
Karim said the link *"goes to their registered phone by SMS or WhatsApp"* (D-051 Q38). **Nothing in
the pinned stack sends an SMS or a WhatsApp message**, and `CLAUDE.md` forbids adding a dependency
without a `decisions.md` entry. Two shapes, and the choice is not Karim's:

1. a provider integration, which is a new dependency, a new secret and a new failure mode; or
2. the endpoint returns the link to the Owner, who sends it from his own phone — no dependency, and
   it matches how Karim already describes handing out temporary passwords (D-049 ruling 4).

**This does not block the story.** Every rule above holds under either, and shape 2 is what slice 1
can demo. It is recorded so the choice is made rather than discovered.

## Questions for Karim
None. **Q38 is closed by D-051.** Two items sit with Nabil and the Architect rather than Karim:
**N7** (delivery channel, above) and **N8** (the link's lifetime — rule 6 requires a finite one and a
configured one; the number is theirs).
