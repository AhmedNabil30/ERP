# KAFF-100 · Bootstrap the first Owner through a one-time setup screen

**Slice:** 1 · **Epic:** Foundation · **Points:** 5 · **Status:** Ready
**Spec:** §9 · **Decisions:** D-044 (ruling 1), D-049 (rulings 3, 4), **D-051 (Q31)**, **D-052 §3 (Q44)**
**Depends on:** nothing. KAFF-101a, KAFF-103 and KAFF-106 name it as a dependency and it is now
`Ready`, so the soft-dependency argument the backlog carried is retired.

## Story
As the first person to open a freshly installed Kaff ERP, I create the Owner account on a setup
screen that exists only while the system has no users at all — so that the very first row in the
audit trail names a person, on a date, rather than naming nobody.

This is the chicken-and-egg the slice-1 kickoff found: *"none of the 26 permissions covers creating
or editing a user"* (`meetings/2026-08-18-slice-1-kickoff.md` §2.1). `UserManage` is the Owner's
alone (D-044 ruling 1), so an empty database has nobody who can create the second user.

## What Karim ruled, and why the reason matters more than the shape
> **Shape B.** A screen that appears **only when the users table is empty**, creates the Owner, and
> locks permanently afterwards. *"I do not want hidden database scripts. My name and account creation
> date must appear naturally in the Audit Trail from day one."* — **D-051 (Q31)**

**The deciding argument is an audit argument, not a convenience one**, and it decides how this story
is built as well as which shape it takes: *"A seeded account has no actor — the first row in the
trail would name nobody"* (D-051). A seed was the other option and it was rejected for that reason.

**This is the most privileged endpoint that will ever exist in this system, and it is
unauthenticated.** Its entire correctness rests on one emptiness check. Rules 4 and 5 are that check
and they are the story.

This also closes finding **F-02**, which was right: the previous version of this story asserted a
seed by citing D-043's *description of the current state* as though it were a ruling.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | `UserManage` is company-wide and held by `Role.Owner` alone, which is why the first Owner cannot be created by any ordinary path | D-044 ruling 1 |
| 2 | The setup endpoint creates exactly one `User`, with `Role.Owner` and **no department** — the Owner is not one of §9's four departments. ⚠️ **UNCITED — WAIVED, Q46. See "Readiness waiver" below** | §9 · **D-051 (Q31)** |
| 3 | It is a real person's account, not a shared `admin` login. Every audit record names an actor, and a shared login makes the trail unreadable — which is the whole reason this shape was chosen | **D-051 (Q31)** · D-049 ruling 4 · CLAUDE.md audit |
| 4 | **The endpoint is served, and succeeds, only while the users table is empty.** Once one user exists it is refused, for good | **D-051 (Q31)** |
| 5 | **"Locks permanently" means the emptiness test itself — there is no flag, no `SetupComplete` row, no configuration switch and no environment variable that re-opens it.** D-051 states this in as many words: *"locks permanently must mean the emptiness test, not a flag anyone can clear."* A flag is a thing an operator can clear; the presence of a user is not | **D-051 (Q31)** |
| 6 | **The check and the insert are one atomic operation, enforced by the database.** Two requests arriving in the same instant must produce one Owner and one refusal — never two Owners. A read-then-write in the handler loses that race, and this is the one endpoint where losing it hands out a second unaudited Owner | **D-051 (Q31)** (*"the check must be atomic against a concurrent second request"*) · CLAUDE.md (the safe floor precedent: enforced by a database constraint, **not** application code) |
| 7 | The Owner **types their own password on this screen**. Nobody else ever knows it | **D-051 (Q31)** (*"my name and account creation date"* — the account is created by its holder) |
| 8 | Therefore **there is no temporary password here and no forced first change.** **The mechanism exists and is named:** the setup screen calls `User.SetOwnPassword`, which leaves `MustChangePassword` false; `User.SetTemporaryPassword` is the other setter and is the one that forces a change. There is no boolean parameter to get wrong [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `SetOwnPassword`, `SetTemporaryPassword`, and the exemption is named in the entity's own remarks at @ `src/Domain/Identity/User.cs` -> `MustChangePassword`]. **Ruled, 2026-08-21:** the forced change of D-049 ruling 4 covers *"an account created for somebody else with a credential its creator knows"*; the Owner types his own password on the setup screen, **so nobody else ever knew it**, the non-repudiation the rule protects is not at risk, and forcing a change would be ceremony. **This is the scope of ruling 4, not an exception to it** — the rule is unchanged and its reach is now written down | **D-052 §3 (Q44)** · D-049 ruling 4, read for what it says · **D-051 (Q31)** |
| 9 | The password is at least 8 characters with no forced complexity — the same rule as every other account | D-049 ruling 3 |
| 10 | Five consecutive failures lock this account for 15 minutes too. **There is no account the lockout exempts**, and no way back in through the setup screen once a user exists (rule 4). The lockout state is on the entity — `FailedSignInAttempts`, `LockedOutUntil`, `RecordFailedSignIn`, `IsLockedOut`, `RecordSuccessfulSignIn`, with the two numbers passed in rather than written there [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `FailedSignInAttempts`, `LockedOutUntil`, `IsLockedOut`, `RecordFailedSignIn`, `RecordSuccessfulSignIn`; migration `20260821221842_UserLockoutAndForcedPasswordChange`] | D-049 ruling 3 · rule 4 |
| 11 | The account records a full name and a phone, because recovery is a link sent to a registered phone (D-051 Q38, KAFF-104) and an Owner with no phone on file has no recovery path at all | **D-051 (Q38)** · KAFF-104 |
| 12 | The password is stored only as a hash. `User.PasswordHash` is `[AuditRedacted]` and must never reach a response, a log or an audit record [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `PasswordHash`, `SecurityStamp` — the attribute is on both] | CLAUDE.md audit · slice 0 `User` |

## Permissions, money, audit, i18n
- **Permissions:** **anonymous — the second and last endpoint in the system that is** (the first is
  sign-in, KAFF-101a). It holds no permission check because there is no identity to check, and every
  other endpoint requires role × assignment (§9). Rules 4, 5 and 6 are what stand in place of a
  permission check, which is why they are stated as properties of the database rather than of the
  handler.
- **Money:** moves no money. It creates the account that approves every financial movement in Kaff
  (§7, §9), which is why this is a 5 and not a 3.
- **Audit:** one `Created` record on `User`. **`ActorUserId` is the newly created Owner itself** —
  the person filling in the form is, by construction, the account being created, and D-051 rejected
  the seed precisely because *"the first row in the trail would name nobody."* A null actor here
  would reproduce the thing the ruling refused. The record carries the creation date, which is the
  second half of what Karim asked to see.
- **i18n:** every string on the screen is a key like any other — `setup.title`, `setup.intro`,
  `setup.field.full_name`, `setup.field.phone`, `setup.field.username`, `setup.field.password`,
  `setup.action.create`, and **`errors.setup.already_completed`**. **No literal in either language**,
  on the first screen as on every other.

  *(Corrected 2026-08-22 under **SM-15**, finding **V-07** / **N-05**. This bullet and `AC-100-B` said
  `errors.setup.already_initialised`, and the submit button said `setup.submit`. **Two names for one
  server refusal is F-08's shape**, and this half is the one that matters: `errors.*` is a
  server-returned `messageKey` the SPA resolves, so a divergence here is a runtime miss, not
  untidiness. The name kept is the one the slice's key register carries
  [Verified: 2026-08-22 @ `ux/slice-1-flows.md` -> `errors.setup.already_completed`], which is also the
  list Frontend builds both catalogues from — **neither spelling exists in `ar.json` or `en.json`
  today** [Verified: 2026-08-22 @ `src/Web/public/locales/ar.json` -> `errors.identity.username_required`
  — the nearest sibling that does exist; the file carries no `errors.setup.*` entry at all], so the cost is this text
  edit and nothing else. `setup.submit` becomes `setup.action.create`, the key S-002 draws
  [Verified: 2026-08-22 @ `ux/slice-1-flows.md` -> `setup.action.create`] and the shape
  `<feature>.action.*` that `ux/rtl-and-i18n.md` §6 fixes. **Backend still owns the errors key** —
  `ux/rtl-and-i18n.md` hard rule 1 — so if Backend names the domain `Error` code differently, the
  catalogues follow the code and this story is the thing that is wrong.)*

## Acceptance criteria
**AC-100-A — an empty system offers the screen, and one Owner comes out of it**
Given a database with no users
When the setup form is submitted with a full name, a phone, a username and an 8-character password
Then one `User` exists with `Role.Owner`, no department, `IsActive` true
And one `AuditRecord` of action `Created` exists for it, naming that user as the actor and carrying the creation timestamp
And the account can sign in immediately with the password that was typed (KAFF-101a)

**AC-100-B — it cannot happen twice** *(fails if the rule is broken)*
Given a database that already contains any user, of any role, active or not
When the setup endpoint is called again
Then it is refused with `errors.setup.already_completed` — **refused, not silently ignored**
And no second user is created and no audit record is written
And the setup screen does not render

**AC-100-C — two simultaneous requests produce one Owner** *(fails if the rule is broken)*
Given a database with no users
When two setup requests are submitted concurrently with different names
Then exactly one succeeds and exactly one is refused
And exactly one `User` row and exactly one `Created` audit record exist
And this holds when the two requests are served by two application instances against one database — the guarantee is the database's, not the process's

**AC-100-D — the lock is the emptiness test, and nothing else** *(fails if the rule is broken)*
Given an initialised system
When the codebase is searched for a setup flag, a `SetupComplete` column, a configuration switch or an environment variable that re-enables the endpoint
Then none exists — the only thing standing between a caller and a second Owner is the presence of a user row

**AC-100-E — deactivating the Owner does not re-open it** *(fails if the rule is broken)*
Given the only user in the system is the Owner, and they are deactivated
When the setup endpoint is called
Then it is still refused — rule 4 counts users, not active users, and a system with one deactivated Owner is a recovery problem, not a fresh installation

**AC-100-F — the Owner types their own password and is not forced to change it** *(ruled — **D-052 §3 (Q44)**)*
Given the Owner created in AC-100-A
When they sign in with the password they typed on the setup screen
Then they reach the application, and are **not** routed to the change-password screen — no temporary credential exists to change
And `GET /api/auth/me` answers, and says no password change is required (KAFF-105a rule 3) — ⚠️ **the
*shape* of that answer is contested, the substance is not.** Whether the fact travels as a field on a
200 or as the absence of a 403 is **N-04 / Q-UX-18, action SM-16, open, BA + Architect**; see
`KAFF-105a` -> *"The `password_change_required` shape is undecided"*. **This criterion is executable
either way** — the Owner is not forced to change anything (D-052 §3), so no refusal is due on any
reading. Marked, not edited. Finding **V-03**.

*This criterion was written before it was ruled on, by reading D-049 ruling 4 for what it says. QA
(QA-4) and UX (Q-UX-17) both flagged that as a story answering its own question, and they were right
to — it was the correct reading, and it was still a reading. **Nabil ruled it on 2026-08-21 and the
reading held**; the criterion now stands on the ruling instead of on the reasoning.*

**AC-100-G — no shared login survives review** *(fails if the rule is broken)* · ⚠️ **the blocklist half is UNCITED — WAIVED, Q45**
Given the setup form
When it is submitted with the username `admin`, `root` or `kaff`, or with an empty full name
Then it is refused — the account must name a person

*The empty-full-name half is cited (rule 3). The three reserved words are not, and are built under the waiver below. If Karim answers Q45 "no", this criterion keeps its empty-full-name half and loses the list.*

**AC-100-H — the password never leaves the database**
Given a successful setup
When the response body, the application log and the audit record are inspected
Then none contains the password or its hash

**AC-100-I — Arabic, RTL, at mobile width**
Given the setup screen at 390px in Arabic
When it renders
Then the direction is RTL, no string is a literal, and there is no horizontal overflow

## Not in this story
Sign-in itself (KAFF-101a). Creating any second user (KAFF-106), which is the ordinary path and needs
`UserManage`. Recovering the Owner's password (KAFF-104) — note that with one Owner and no second
`UserManage` holder, an Owner who forgets their password before creating a second user has **no**
route back in; that is a real consequence of rules 4 and 5 and it is stated here rather than fixed by
inventing a back door. Seeding a project — slice 1 needs one to assign against; see KAFF-113's
"Not in this story".

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
| **Rule 2** — the first Owner carries no department. Cited to *"§9 · D-051 (Q31)"*; **D-051 Q31 never mentions a department** and §9 does not exclude the Owner from having one | **Q46**, open, for Karim |
| **AC-100-G**, the blocklist half — `admin`, `root`, `kaff` as reserved usernames. Rule 3 argues the account must name a person, which is a different claim from a list of forbidden words | **Q45**, open, for Karim |

**The waiver lets the story be built. It does not answer either question.** Q45 and Q46 stay open in
`stories/questions-for-karim.md` until Karim rules, and if he rules the other way these two rules
change — the cost of that lands on the Architect by his own signature, which is the point of a signed
waiver rather than a shrug.

## Questions for Karim
**Q31 is closed by D-051. Q44 is closed by D-052 §3.** Two open, neither of which blocks this story
— both are rules this story states with no source behind them, raised rather than left to look ruled:

- **Q45** — are `admin`, `root` and `kaff` reserved? **AC-100-G names that exact blocklist and cites
  nothing.** Rule 3 argues that the account must name a person, which is a different claim from a list
  of forbidden words. If the answer is no, AC-100-G keeps its empty-full-name half and loses the list.
- **Q46** — does the first Owner carry no department? **Rule 2 says so and cites *"§9 · D-051
  (Q31)"*; D-051 Q31 never mentions a department** and §9 does not exclude the Owner from having one.
  Probably right, sourced to a ruling that does not say it.
