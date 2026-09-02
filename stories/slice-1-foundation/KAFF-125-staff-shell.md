# KAFF-125 · The staff shell: session resolution, chrome, and role-based landing

**Slice:** 1 · **Epic:** Foundation · **Points:** 3 · **Status:** **Cut, 2026-09-02. Not in any sprint,
and not marked Ready or BLOCKED against one — whether it is built in sprint 2 is a scope question
standing with Nabil right now** (see *Open questions* below). Cutting this ticket is not committing to
it.
**Spec:** §8, §9, §12 · **Decisions:** D-050, **D-072 §2**, D-051 (Q33)
**Depends on:** KAFF-101a, KAFF-101b, KAFF-105a

> **Why this story exists, verbatim from Nabil, 2026-09-02:** *"KAFF-105b (Backend) remains the API
> payload ticket. It technically satisfies the backend portion of the ACs the moment it returns the
> correct role/permission data structure. A dedicated frontend ticket must be cut for the visual shell
> itself — the layout, sidebar, header, and role-based routing. **You cannot discharge a UI rendering
> dependency with a JSON response.**"*
>
> `AC-101b-A` and `AC-101b-D` were deferred onto KAFF-105b and KAFF-115. `meetings/2026-09-01-sprint-2-refinement.md`
> §3.1 found neither can discharge them: KAFF-105b renders nothing, and KAFF-115 builds one project's
> team panel (S-009b), not the project list (S-009a) HR is meant to land on. Both criteria are
> re-pointed at this story in a dated amendment to `KAFF-101b`, made in this same pass.

## Story
As a member of staff who has just signed in, I arrive inside a real application — a header, a side
navigation, and my role's own landing screen — instead of a blank page or a bare status endpoint,
because a `200` from `GET /api/auth/me` is not the same thing as somewhere to stand.

## What this story can actually build today, and what it cannot

**The entire API exposes three `GET` routes: `/api/auth/me`, `/api/health` and `/api/setup`**
[Verified: 2026-09-02 — searched `src/Api/Features/*/*/Endpoint.cs` for `MapGet`; the three are
`src/Api/Features/Auth/WhoAmI/Endpoint.cs`, `src/Api/Features/Health/GetHealth/Endpoint.cs`,
`src/Api/Features/Setup/GetSetupAvailability/Endpoint.cs`]. `ux/navigation.md` -> `Landing summary`
rules a slice-1 landing for every one of the nine roles. Read against the routes that exist, and
against what stories exist to render them, the honest table is this — not the one costed in
`meetings/2026-08-30-sprint-2-open.md` §4.2, which this refinement (§3.1) found understates the hole:

| Role | Ruled landing | Can this story render it? |
|---|---|---|
| Finance, TechnicalOffice, SiteEngineer, HeadOfDesign | S-005 My profile | **Partially.** `GET /api/auth/me` carries `displayName`, `role`, `department` and `operationsSubDepartment` today [Verified: 2026-09-02 @ `src/Api/Features/Auth/WhoAmI/Response.cs`]. But `ux/screen-inventory.md` -> S-005 also requires *"the projects I am assigned to with my level"*, and that field does not exist on the response yet — it is KAFF-105b's, and KAFF-105b is **Ready but not built** [Verified: 2026-09-02 @ `src/Api/Features/Auth/WhoAmI/Response.cs` — the record carries no assignment or project field]. This story renders the identity half now; the assignment half renders the day KAFF-105b ships, with no change to this story's own code |
| Owner | S-006 User list | **No. There is no list-users route** [Verified: 2026-09-02 — the `Users` feature folder exposes `CreateUser`, `DeactivateUser`, `MoveUserDepartment`, `ReactivateUser`, `ChangeUserRole`; none is a `GET`]. Nothing feeds S-006 |
| MarketingSales | S-011 Client list | **No.** Clients are KAFF-119…124, deferred out of sprint 1 entirely, and no client route exists |
| Hr | S-009a HR project list | **Neither reading is decided, and I am not picking one — see *Open questions* below** |

**No criterion below asserts S-006, S-011 or S-009a rendering real data**, per `agents.md` §3c's rule
cutting both ways: a criterion that cannot pass is as bad as one that cannot fail, and nothing feeds
those three today.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | There are exactly **three** shells — staff, site, portal. **This story builds only the staff shell.** Header: app name, locale switch, account menu. Side navigation at inline-start on desktop, collapsing to a drawer that slides in **from the right** at 390px. Do not invent a fourth shell | `ux/navigation.md` -> `Shell shapes` |
| 2 | The shell has **three** session states, not two — `resolving`, `signed-in`, `signed-out`. `resolving` renders a neutral boot surface only: app name, locale switch, progress indicator. Never the sign-in form, never the staff chrome, never an empty shell | `ux/navigation.md` -> `The shell has three session states, not two — D-050` · **D-050** |
| 3 | **Route guards await resolution.** A guard that resolves against `null` sends a signed-in user to the sign-in screen and loses the URL they typed | `ux/navigation.md` (same section) · D-050 |
| 4 | `GET /api/auth/me` is the only source of the shell's contents. Nothing about the session is stored client-side, and sign-out does not clear anything client-side either — the server clears the `HttpOnly` cookie, and the shell drops its in-memory profile and returns to `resolving` | D-050 · KAFF-105a |
| 5 | **S-004, the session-resolution and landing dispatcher, is not a screen anyone sees.** It calls `/api/auth/me`, decides the session state, and routes to the role's landing | `ux/screen-inventory.md` -> `S-004` |
| 6 | Role-based routing sends each signed-in role to its ruled landing. Built from the permission set returned by `/api/auth/me`, never from `switch (role)` — department and per-project seniority are independent axes a role switch cannot see | `ux/navigation.md` -> `Landing summary`, `Navigation is built from the permission set, not from switch (role)` |
| 7 | **`mustChangePassword` is a field on the `/api/auth/me` response, never a refusal.** A user for whom it is `true` is routed to the change-password screen (S-003) and reaches nothing else until it is `false` | **D-072 §2** [Verified: 2026-09-02 @ `src/Api/Features/Auth/WhoAmI/Response.cs` -> `MustChangePassword`] |
| 8 | Arabic is the interface language and RTL is the primary direction, not a mirror — logical properties throughout, tested at 390px. No hardcoded string in either language | CLAUDE.md · `ux/navigation.md` -> `Shell shapes` |
| 9 | The shell enforces no permission. It is presentation; a route a role should not see is reached, sent to the server, and refused there. Hiding it is convenience only | CLAUDE.md · `ux/navigation.md` -> `What hiding is and is not` |

**This does not start from nothing, and the story is not a rewrite of what already exists.**
`AuthService` already carries the `resolved` / `current` / `session` signals the three states above are
built from, and `mustChangePasswordGuard` already implements rule 7's routing for the one protected
route that exists today (`AC-101b-F`) [Verified: 2026-09-02 @ `src/Web/src/app/core/auth/auth.service.ts`
-> `AuthService`; @ `src/Web/src/app/core/auth/must-change-password.guard.ts` ->
`mustChangePasswordGuard`]. This story is the chrome, S-004's dispatch, and the per-role landing routes
built on top of that service — not a second implementation of it. **One stale comment to note, not to
fix here (`src/` is not the BA's):** `src/Web/src/app/app.routes.ts`'s wildcard route still attributes
the staff shell to *"KAFF-105b's shell"* [Verified: 2026-09-02 @ `src/Web/src/app/app.routes.ts` ->
the `path: '**'` comment] — exactly the confusion Nabil's ruling above corrects. Routed to Frontend to
fix when this story is built.

## ⚠️ `ux/navigation.md` is stale on `mustChangePassword`, and this story is written against the ruling, not the file
`ux/navigation.md` -> `Navigation is built from the permission set...` still reads: *"A user still
holding a temporary password does not get this payload at all — the call is refused with
`errors.auth.password_change_required`"* [Verified: 2026-09-02 @ `ux/navigation.md` — the paragraph
following the `Me` interface]. **That is the refusal reading D-072 §2 replaced on 2026-08-24**, and the
code matches D-072 §2, not this paragraph [Verified: 2026-09-02 @
`src/Api/Features/Auth/WhoAmI/Response.cs` -> `MustChangePassword`]. `meetings/2026-09-01-sprint-2-refinement.md`
bucket 2 already routed this correction to UX and BA, and it was not done. **Rule 7 above is written
against D-072 §2. `ux/navigation.md` is not corrected here — it is not this agent's file — and this
staleness is flagged again in this session's report so it does not get lost a second time.**

## Permissions, money, audit, i18n
- **Permissions:** none of its own. The shell renders whatever `GET /api/auth/me` already decided;
  every route it links to is authorised again by the server (CLAUDE.md, rule 9 above).
- **Money:** moves none, shows none.
- **Audit:** none. It writes no state.
- **i18n:** `shell.header.app_name`, `shell.nav.*` per role from `ux/navigation.md`'s per-role tables,
  `shell.locale.switch`, `shell.account.menu`, `shell.boot.loading` for the `resolving` surface. No
  literal in either language.

## Acceptance criteria
**AC-125-A — the boot surface, not the shell, while resolution is pending**
Given the application is loading and `GET /api/auth/me` has not yet answered
When the shell mounts
Then it renders only the neutral boot surface — app name, locale switch, progress indicator — never the sign-in form, the staff chrome, or an empty shell

**AC-125-B — a route guard waits for resolution, it does not race it** *(fails if the rule is broken)*
Given a signed-in user requests a deep link before `/api/auth/me` has resolved
When the guard runs
Then it awaits resolution before deciding, and the user reaches the URL they requested rather than being bounced to sign-in and losing it

**AC-125-C — the four profile-only roles land on S-005** *(fails if the rule is broken)*
Given an active user of Finance, TechnicalOffice, SiteEngineer or HeadOfDesign, freshly signed in
When the shell resolves
Then they land on S-005, showing their display name, role and department from `GET /api/auth/me`
And no project or assignment is shown, because `/api/auth/me` carries neither today — that is KAFF-105b's field, not yet built, and this criterion is not rewritten the day it ships

**AC-125-D — a forced password change pre-empts every landing** *(fails if the rule is broken)*
Given `mustChangePassword: true` on the `/api/auth/me` response
When the shell resolves
Then the user is routed to the change-password screen and reaches no landing, no navigation item and no other route until it is `false`

**AC-125-E — sign-out returns the shell to `resolving`, and nothing survives client-side** *(fails if the rule is broken)*
Given a signed-in user inside the staff shell
When they sign out
Then the shell drops its in-memory profile and returns to `resolving`
And no token, session id or profile fact is found in `localStorage` or `sessionStorage`, before or after

**AC-125-F — Arabic, RTL, at mobile width**
Given the staff shell at 390px in Arabic
When it renders
Then direction is RTL, the drawer slides in from the right, no string is a literal, and there is no horizontal overflow

## Not in this story
S-006 (no endpoint feeds it), S-011 (KAFF-119…124, deferred), S-009a's actual rendering (open question,
below). The site shell and the portal shell — different shells, different stories, later slices. Any
project-scoped navigation item — projects arrive in slice 4. KAFF-105b's payload and KAFF-115's panel
themselves, which this story reads but does not build.

## Open questions — not Karim's, and not decided here

| # | Question | Owner |
|---|---|---|
| 1 | **The Owner has no S-006 endpoint and no story builds one. What does the Owner land on in this sprint** — nothing, a stated "not built yet" message, or does S-006 get pulled forward? Inventing an interim landing here would be exactly the kind of plausible invention `agents.md` calls this project's most expensive failure mode | **UX, then Nabil** |
| 2 | **MarketingSales's S-011 needs the client stories (KAFF-119…124), deferred out of sprint 1. What does MarketingSales land on until they ship?** Same shape as question 1, not answered the same way by default | **UX, then Nabil** |
| 3 | **Does HR's S-009a render from the shared `GET /api/auth/me` (KAFF-105b's shape, rule 6 there), or does it need its own `/api/hr/projects` route with unshared response types, as `ux/screen-inventory.md` -> S-009a and `ux/navigation.md` -> "How HR reaches a project at all" both describe** *("on HR's own routes against its own API")*? **Neither is built today, and they are not the same shape** — KAFF-105b rides the endpoint every role calls; `ux/`'s description is a dedicated HR-only API. This is not decided here | **UX + BA, then Nabil** |
| 4 | **B3-8, carried from the 2026-09-01 refinement, unresolved and named rather than answered here: who holds the `GET /api/auth/me` result inside this shell, and what invalidates it?** It decides whether a revoked assignment leaves the navigation wrong for one second or for the rest of the session, and `AC-105b-I` requires the list to be empty "on the next call" without saying what triggers that call from inside a running shell | **Architect** |

## Questions for Karim
None. Every open item above belongs to UX, the Architect or Nabil's scope call — none is a business
rule for Karim.
