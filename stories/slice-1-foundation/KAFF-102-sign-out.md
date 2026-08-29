# KAFF-102 · Sign out

**Slice:** 1 · **Epic:** Foundation · **Points:** 2 · **Status:** **ACCEPTED 2026-08-27 at `559ac45`, then the code moved underneath the verdict.** Sign-out resolves its caller through `LiveSession.ResolveAsync`, which calls `MayHoldStaffSession` — changed by `ca4db6c` (D-095). **Not re-verified at HEAD**
**Spec:** §9 · **Decisions:** **D-049 (ruling 2)**, **D-050**, **D-051 (N5)**
**Depends on:** KAFF-101a

> **Re-estimated 3 → 2 on 2026-08-21.** The 3 carried the cost of whatever N5 was going to require.
> N5 is answered — **there is no session table** — and what is left is clearing a cookie and writing
> an audit record.

## Story
As a signed-in user, I sign out **on this device**, so that the person who picks up my phone or sits
at my desk is not me as far as the system is concerned — and so that signing out here does not sign
my colleague's shared office machine out from under him.

Kaff's site engineers share vehicles and offices, and the daily log is designed mobile-first (§8,
CLAUDE.md). Sign-out is not a formality here.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | Sign-out ends **this device's** session only. Signing out on the site phone does not sign the same user out on the office computer | D-049 ruling 2 |
| 2 | **Sign-out clears the cookie, and that is the whole mechanism.** There is no session table, so the token itself is not revoked — a caller who kept the cookie value can use it until it expires. **This is a known, accepted limit, not an oversight** | **D-051 (N5)** |
| 2a | **The limit is bounded by two things that do exist**: the session expires after 30 minutes of inactivity (rule 5 of KAFF-101a), and any act that must kill sessions everywhere — a password change, a reset, a deactivation — rotates `User.SecurityStamp` [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `StorePasswordHash`, `Deactivate`; the rotation lives in the private `StorePasswordHash`, reached by `SetOwnPassword` and `SetTemporaryPassword` — **`SetPasswordHash` no longer exists**], which KAFF-101a validates per request [Verified: 2026-08-22 @ `src/Infrastructure/Authorization/PermissionSubjectReader.cs` -> `ReadAsync`]. **Sign-out is deliberately not one of those acts**, because rotating on sign-out would sign the user out on every other device, which is exactly what ruling 2 forbids | **D-051 (N5)** · D-049 ruling 2 · KAFF-101a rules 11, 11a |
| 3 | The response clears the `__Host-kaff-auth` cookie — same name, same path `/`, same attributes, expired. A cookie cleared with different attributes is not cleared at all | D-050 |
| 4 | Sign-out is available to every authenticated role including `Role.Client` | §9, §12 |
| 5 | Signing out never deactivates the account and never touches the password | §9 |
| 6 | **A password change and a deactivation are the two acts that do end every session everywhere.** Sign-out is deliberately not one of them | D-049 ruling 2 · KAFF-101a rule 11 |
| 7 | Signing out when already signed out is not an error worth a refusal — the outcome the caller asked for already holds | §9 — no source requires a refusal, and inventing one would be inventing a rule |

## Permissions, money, audit, i18n
- **Permissions:** authenticated, any role. No project, no assignment.
- **Money:** moves no money.
- **Audit:** a record naming the user, the time and the request path. The trail must be able to
  answer *"was he signed in when that extract was approved"*, which needs both ends of the session.
- **i18n:** `auth.action.sign_out`, `auth.signed_out`. *(These were `auth.logout` and
  `auth.logout.confirmed` until 2026-08-22. `<feature>.action.*` is the shape for a scoped button
  [Verified: 2026-08-22 @ `ux/rtl-and-i18n.md` -> `<feature>.action.*`], and the sibling
  `auth.action.sign_in` is already the key the screens use [Verified: 2026-08-22 @
  `ux/slice-1-flows.md` -> `auth.action.sign_in`]; `auth.signed_out` follows
  `auth.password.changed`'s shape for a completed-action message. **`ux/slice-1-flows.md` S-003 still
  says `auth.logout` in prose** — that mention is UX's to change and is flagged to the Scrum Master,
  not edited here. Corrected under **SM-15**, finding **V-07**.)*

## Acceptance criteria
**AC-102-A — the browser stops being signed in**
Given I am signed in in a browser
When I sign out and then call any authenticated endpoint from that browser
Then the request is refused with `errors.auth.not_authenticated`, because the cookie is gone

**AC-102-B — and the limit is asserted, not assumed** *(fails if somebody quietly adds a session table)*
Given the same sign-out
When the cookie value captured beforehand is replayed by a tool that ignores `Set-Cookie`, within the inactivity window
Then it **is still accepted** — this is D-051 (N5)'s accepted trade, and the test exists so that the day it stops being true, somebody has decided to make it stop rather than drifted into it

**AC-102-C — my other device is untouched** *(fails if the rule is broken)*
Given I am signed in on two devices
When I sign out on one
Then the other device's next request still succeeds

**AC-102-D — the cookie is actually cleared**
Given a sign-out
When the response headers are read
Then `__Host-kaff-auth` is cleared with the same name, path `/`, `Secure` and `SameSite=Strict` it was set with

**AC-102-E — sign-out does not disable the account**
Given I have signed out
When I sign in again with the same credentials
Then I am signed in, and `IsActive` was never changed

**AC-102-F — a portal user can sign out**
Given I am signed in as `Role.Client`
When I sign out
Then the same guarantees hold, and nothing about another client is exposed in the response

## Not in this story
Session expiry by inactivity — KAFF-101a rule 5, 30 minutes (D-049 ruling 2). Deactivating a user,
which is a different act with a different actor (KAFF-110). **"Sign out everywhere" as a deliberate
user-facing action:** Karim ruled sessions are per-device, so this is now a real candidate feature —
and it is a new story, not a silent addition here. Nobody at Kaff has asked for it.

## N5 is answered — and it answered "no", which is why this story shrank
Ruling 2 asks for two things that pull in opposite directions on a stateless token: per-device
sign-out, and a password change that kills **every** session. The Architect's answer (**D-051 N5**):

> Routine per-device sign-out clears the cookie in that browser. Global kill — stolen phone, password
> change — rotates `User.SecurityStamp`, and the API rejects any token carrying the old one.

**No session table.** D-051 accepts the known limit rather than hiding it: with no per-session
identity there is no way to revoke *one other* device, so losing a phone means signing out
everywhere. That is the right trade for a first-party SPA on one origin, and a session table later is
additive.

**The half this story depends on is KAFF-101a rule 11a, and it now EXISTS.** Built, decisions.md
D-053 §1: `IPermissionSubjectReader.ReadAsync` takes the stamp and refuses a mismatch or an absence,
so rotation invalidates every token for that user at once
[Verified: 2026-08-22 @ `src/Infrastructure/Authorization/PermissionSubjectReader.cs` -> `ReadAsync`].

> **Corrected 2026-08-22.** This paragraph said the comparison did not exist and that the story was
> "only correct if that one is built". It is built. Do not build it again — verify it instead:
> `Rotating_the_security_stamp_kills_every_existing_session` and `A_request_with_no_security_stamp_is_refused`
> [Verified: 2026-08-22 @ `tests/Api.Tests/PermissionMechanismTests.cs` -> `Rotating_the_security_stamp_kills_every_existing_session`, `A_request_with_no_security_stamp_is_refused`].

## Questions for Karim
None. Ruling 2 answers the business question; **D-051 (N5)** answers the mechanism.
