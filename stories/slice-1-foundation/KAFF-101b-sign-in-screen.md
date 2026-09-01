# KAFF-101b · The staff sign-in screen, and where each role lands after it

**Slice:** 1 · **Epic:** Foundation · **Points:** 3 · **Status:** **BUILT, NOT VERIFIED.** The screen shipped `f2b995b` (D-091) and is the first thing in this project that ever rendered; it sets the conventions every later screen copies. `AC-101b-F` closed with KAFF-103's screen, `332c160` (D-092), and was observed end to end. **`AC-101b-A` (the staff shell) and `AC-101b-D` (HR lands on the team surface) are deferred to KAFF-105b and KAFF-115 and are not closed** — and neither story currently carries a criterion that builds a shell, which is a refinement question for the BA and a scope question for Nabil (`meetings/2026-08-30-sprint-2-open.md`). ⚠️ **Sharpened 2026-09-01** (`meetings/2026-09-01-sprint-2-refinement.md` §3.1): **`AC-101b-D` fails the identical arithmetic `AC-101b-A` does, and it was not seen until today.** HR's landing is **S-009a**, the project *list* (`ux/navigation.md` -> `Landing summary`), while **KAFF-115 builds S-009b**, one project's *team panel* — `AC-115-H` opens *"the Project Team screen **for project A**"*, which is per-project, not the list `AC-101b-D` requires HR to land on [Verified: 2026-09-01 @ `stories/slice-1-foundation/KAFF-115-project-team-panel.md` -> `AC-115-H`]. The criterion is deferred onto a story that does not build the screen it names, exactly as `AC-101b-A` is. **No reading is picked here** — this widens Nabil's open question on `AC-101b-A` rather than answering it; see the same note in `KAFF-115`. No independent session has looked at this commit
**Spec:** §9, §12 · **Decisions:** D-035, D-049 (ruling 3), **D-050**, **D-051 (Q33)**
**Depends on:** KAFF-101a, KAFF-105a

> **Split from KAFF-101** on 2026-08-21, closing QA's finding **F-22**. The API half is KAFF-101a.
> This half was blocked on **Q33** and **Q33 is now answered**.

## Story
As a member of Kaff staff standing in front of the login screen, I sign in and arrive somewhere that
belongs to me — and a client never reaches this screen at all, because a client signs in somewhere
else entirely.

## What Karim ruled
> **The client portal is a separate host.** Clients sign in at a different URL. *"Their portal must be
> a completely isolated interface."* — **D-051 (Q33)**

This strengthens D-035, which found the portal one careless endpoint away from leaking: **a separate
host makes the boundary infrastructural rather than a matter of every future endpoint remembering.**
It also settles `ux/questions.md` Q-UX-9's requirement — a client must land in the portal shell,
*"never in the staff shell, not even for one frame, not even empty"* — by removing the staff shell
from the client's path altogether rather than by racing a redirect against a render.

**What it does not settle**, and what does not block this story: whether the portal is a separate
deployment or the same API behind a second origin. That is **N7** (see below) and it lands in slice 8.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | **This screen is the staff front door and serves staff only.** Clients sign in at a different host, on their own screen, which is slice 8 (KAFF-810) | **D-051 (Q33)** · §12 · D-035 |
| 2 | **Nothing on this screen names, links to or hints at the portal**, and nothing on the portal links here. *"A completely isolated interface"* means a client never learns this address exists | **D-051 (Q33)** · D-035 |
| 3 | **A `Role.Client` credential presented here never reaches the staff shell** — not the empty shell, not for one frame. In slice 1 there is nothing to route them to, so the outcome is a refusal at the staff origin, not a redirect | **D-051 (Q33)** · `ux/questions.md` Q-UX-9 · KAFF-101a rule 16 (amended) |
| 4 | The screen imposes nothing the server does not. Whatever it shows about a refusal comes back as a `messageKey` | §9 (*"hiding UI elements is presentation, not security"*) · `problem-details.ts` |
| 5 | The password field demands a minimum of 8 characters and **nothing else** — no strength meter, no symbol rule, no digit rule. A strength meter is a policy statement wearing a progress bar | D-049 ruling 3 |
| 6 | A refusal reads the same for a wrong password, an unknown username and a locked account, because the server returns one `messageKey` for all three | KAFF-101a rules 13–14 |
| 7 | The screen stores no token, reads no cookie, and holds no session in `localStorage` or `sessionStorage`. What it knows about the signed-in user comes from `GET /api/auth/me` | D-050 · KAFF-105a |
| 8 | A user whose password was set for them is taken straight to the change-password screen and can reach nothing else | D-049 ruling 4 · KAFF-103 |
| 9 | **An `Role.Hr` user lands on the Project Team surface, not on a project dashboard** — HR holds no `ProjectRead` and the two surfaces are separate | **D-051 (Q32)** · KAFF-105b · KAFF-115 |
| 10 | Every other staff role lands in the staff shell, whose contents come from `GET /api/auth/me` and nowhere else | D-050 · KAFF-105a, KAFF-105b |
| 11 | Arabic, RTL, at mobile width — engineers sign in on a phone, on site | CLAUDE.md · §8 |

## Permissions, money, audit, i18n
- **Permissions:** none — the screen is anonymous. Everything it can reach afterwards is decided by
  the server (§9).
- **Money:** moves no money.
- **Audit:** none of its own; KAFF-101a writes the records.
- **i18n:** `auth.login.title`, `auth.login.username`, `auth.login.password`, `auth.login.submit`,
  and the `errors.auth.*` keys KAFF-101a lists. **No literal in either language.**

## Acceptance criteria
**AC-101b-A — a staff sign-in arrives at the staff shell**
Given an active Finance user
When they sign in
Then they arrive at the staff shell, and the shell's contents come from `GET /api/auth/me`

**AC-101b-B — a client never sees the staff shell** *(fails if the rule is broken)*
Given a valid `Role.Client` credential
When it is submitted at the staff sign-in screen
Then the staff shell never renders — not empty, not for one frame
And the response is a refusal, not a redirect into the application

**AC-101b-C — the portal is not discoverable from here** *(fails if the rule is broken)*
Given the rendered staff sign-in page and its bundle
When both are searched
Then neither contains the portal's address, a link to it, or a "are you a client?" affordance

**AC-101b-D — HR lands on the team surface** *(fails if the rule is broken)*
Given an active `Role.Hr` user
When they sign in
Then they land on the Project Team surface
And no project dashboard route is reachable from their navigation, and requesting one directly is refused by the server

**AC-101b-E — the screen imposes only what was ruled** *(fails if the rule is broken)*
Given the password field
When a password of 8 lower-case letters is typed
Then the form submits — no client-side rule refuses it, and no strength meter is rendered

**AC-101b-F — a forced change cannot be walked around** *(fails if the rule is broken)*
Given a user signing in with a password the Owner set
When they attempt to navigate anywhere but the change-password screen
Then they cannot, and a reload returns them to it

**AC-101b-G — one refusal for three causes** *(fails if the rule is broken)*
Given a wrong password, an unknown username and a locked account
When each is submitted
Then the screen shows the same message for all three, because the server sent the same `messageKey`

**AC-101b-H — Arabic, RTL, at mobile width**
Given the login screen at 390px in Arabic
When it renders
Then the direction is RTL, no string is a literal, and there is no horizontal overflow

## Not in this story
The endpoint (KAFF-101a). The portal's own sign-in screen and shell, which live on the other host and
are slice 8 (KAFF-810). Sign-out (KAFF-102). The Project Team surface itself (KAFF-115).

## The one thing Q33 left open — **N7**, for Nabil and the Architect
A separate host is ruled. **Separate deployment, or the same API behind a second origin, is not.**
The second still needs D-050's cookie and CORS worked through: `__Host-` forbids a `Domain`
attribute, so a second origin means a second cookie and a second session boundary, and CORS must name
both origins explicitly because credentials are in play. Recorded as **N7** in
`questions-for-karim.md`. It does not block this story — this screen is the staff host either way —
and it must be settled before **KAFF-810** in slice 8.

## Questions for Karim
None. **Q33 is closed by D-051.**
