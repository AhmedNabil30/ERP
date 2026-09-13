# KAFF-215 · A sidebar that lists every destination the session reaches

<!-- kaff id=KAFF-215 slice=2 points=5 state=NOT-BUILT verdict=none at=- on=2026-09-13 -->

**Slice:** 2 (Masters) · **Epic:** Masters — nav shell · **Points:** 5. Not a 1: this is a second
permission-driven table alongside `landing.ts`'s, a completeness test that walks `app.routes.ts`
against it, i18n keys in both locales for every destination, an active-row mechanism, and a drawer
that still has to work at 390px with more than one row in it. Comparable in shape to `KAFF-207`/`209`/
`211` (5 each), not to a single-screen CRUD story. **Status:** **NOT-BUILT.** Cut 2026-09-13 by the BA
from Nabil's staging defect of the same date.
**Spec:** — (no `spec.md` section governs navigation; `ux/navigation.md` and CLAUDE.md's Angular
conventions do) · **Decisions:** none yet — this story does not invent one, see Questions
**Register:** no `Q` — this is a defect repair with the requirement stated verbatim by Nabil, not an
open business question
**Screens:** every screen slice 2 shipped and could not be clicked to: `ux/screen-inventory.md` →
`S-017` (catalogue), `S-021`/`S-022` (أبواب), `S-023` (employees), `S-026`/`S-027` (day labour),
`S-028`/`S-029` (subcontractors), `S-030` (suppliers) — plus `S-015` (audit trail), reachable by URL
and gated since slice 1 but never given a row either, found while reading `app.routes.ts` for this
story, not asked for by Nabil in terms
**Owner:** Frontend
**Depends on:** nothing new. Every guard and every route this story adds a row for is already merged
(`KAFF-125` `app.routes.ts`, `KAFF-202`/`203`/`204`/`205`/`206`/`213`/`214`, `KAFF-207`/`208`/`209`/
`210`, `KAFF-211`, `KAFF-212`).

## Story
As any staff role signed in to Kaff, I see, in the sidebar, every screen my session's permissions
actually let me open — not the one screen `landing.ts` happened to redirect me to — because a built,
routed, deployed screen that cannot be reached by clicking anything is not shipped, it is buried; six
slice-2 screens are buried today and Nabil found it by logging in as Owner and seeing only the user
list.

## The defect, verbatim in substance

Logged in as Owner on staging 2026-09-13, Nabil saw only the user list. `src/Web/src/app/app.html`
renders exactly **one** nav row, from `navLabelKey()` / `navPath()` — its own comment names it *"the
one entry that exists in slice 1."* `src/Web/src/app/core/navigation/landing.ts` maps a session's
permissions to a single `Landing`, by design (`KAFF-125` rule 6): one destination, for the redirect
after sign-in. That was correct when one destination was all that existed. It is not correct now:
catalogue, أبواب, employees, day labour, subcontractors and suppliers are all built, routed and
deployed, and none of them has a row anywhere a user can click.

**`landing.ts` stays exactly as it is.** It answers a different question — "where does this session
land right after sign-in" — and nothing about that question changes. This story adds a second,
separate list: every row the sidebar shows, derived from the same permission source `landing.ts`
already reads (`GET /api/auth/me`'s evaluated `permissions` set), never from a hand-typed list in the
template and never from `switch (role)` (`ux/navigation.md` → *"Navigation is built from the
permission set, not from `switch (role)`"*).

## What already exists, and what this story adds

| Already built | Evidence |
|---|---|
| Every route this story rows: `/clients`, `/users`, `/audit`, `/catalogue`, `/babs`, `/employees`, `/projects/:projectId/day-labour`, `/subcontractors`, `/suppliers` | [Verified: 2026-09-13 @ `src/Web/src/app/app.routes.ts`] |
| A guard per route reading a company-wide or project-scoped permission off the session | [Verified: 2026-09-13 @ `src/Web/src/app/core/auth/*.guard.ts` — `client-manage`, `user-manage`, `audit-read`, `catalogue-manage`, `bab-manage`, `employee-manage`, `day-labour-site-manage`, `subcontractor-manage`, `supplier-manage`] |
| The single hand-listed row, its `is-active` class always on, and the mobile drawer shell (toggle, backdrop, RTL slide-in) | [Verified: 2026-09-13 @ `src/Web/src/app/app.html` lines around `side-nav`, and its own comment: *"the one entry that exists in slice 1"*] |
| The permission → single-landing table, and the rule it follows | [Verified: 2026-09-13 @ `src/Web/src/app/core/navigation/landing.ts` -> `RULED_LANDINGS`, `landingFor`, `navLabelKeyFor`, `navPathFor`] |

**What does not exist is any list of more than one row**, and no mechanism that walks
`app.routes.ts` to check one was not missed.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | The sidebar lists **every** destination the session's evaluated permission set actually reaches — not the single post-login landing | Nabil, staging defect, 2026-09-13 |
| 2 | `landing.ts`'s single landing is **unchanged** and used **only** for the post-login redirect. This story does not touch `RULED_LANDINGS`, `landingFor`, `navLabelKeyFor` or `navPathFor` | Nabil, verbatim |
| 3 | The row list is built from the **same permission source** `landing.ts` reads — `Session.permissions`, the evaluated company-wide set `GET /api/auth/me` returns — never a `switch (role)` and never a second hand-typed copy of the permission catalogue | `ux/navigation.md` → *"Navigation is built from the permission set, not from `switch (role)`"* |
| 4 | **No hand-listed route in the template.** The template iterates a list a `.ts` file produces; it does not itself name `/catalogue` or any other path | Nabil, verbatim |
| 5 | **A routed, permission-gated screen with no corresponding row is a test failure**, not a silent gap — the exact failure mode this story exists to close off from recurring | Nabil, verbatim (*"a new screen cannot ship without a row"*) |
| 6 | Row order is **stable** — the same session produces the same order on every load, and the order does not depend on object/map iteration order | Nabil, verbatim |
| 7 | A permission the session does not hold shows **no** row for the screen(s) that permission gates. Hiding is presentation, not the security control — the guard and the server refusal underneath are unchanged | CLAUDE.md · `ux/navigation.md` → *"Never enforce permissions in the frontend alone"* |
| 8 | The row for the screen the router is currently on is marked active | Nabil, verbatim |
| 9 | The mobile drawer (toggle, backdrop, RTL slide-in from the right at 390px) keeps working with an arbitrary number of rows, not just one | Nabil, verbatim · `ux/navigation.md` → Staff shell shape |
| 10 | Every label is an i18n key, present in both locales — no hardcoded string, no locale with a row the other lacks | CLAUDE.md · `ux/rtl-and-i18n.md` |

## Permissions, money, audit, i18n
- **Permissions:** this story reads `Session.permissions` — it grants nothing and checks nothing new
  server-side. Every route it rows already has its own guard and its own server-side `403`; this story
  cannot make a screen reachable that the guard and the API do not already allow, and must not be built
  as if it could.
- **Money:** none. Presentation only.
- **Audit:** none. No state change.
- **i18n:** one key per row (existing `nav.*` keys already used by the single hand-listed entry today —
  `nav.clients`, `nav.users`, `nav.catalogue` — plus new ones this story needs: `nav.audit`,
  `nav.babs`, `nav.employees`, `nav.subcontractors`, `nav.suppliers`, and whatever key day labour's row
  takes once the open question below is answered), each present in both the Arabic and English
  translation files.

## Acceptance criteria

**AC-215-A — rows come from a permission table, not a hand list** *(fails if the rule is broken)*
Given the sidebar's row source
When its implementation is read
Then it is a data table keyed by permission (the same shape as `landing.ts`'s `RULED_LANDINGS`), and
`app.html` contains no hand-typed route path or hand-typed permission name of its own

**AC-215-B — every routed, permission-gated screen has a row, or the test reddens** *(fails if the rule is broken)*
Given the top-level entries in `app.routes.ts` that carry a permission guard (today: `/clients`,
`/users`, `/audit`, `/catalogue`, `/babs`, `/employees`, `/subcontractors`, `/suppliers` — day labour
held, see Questions)
When a test compares that route list against the row table
Then the test fails if a guarded top-level route has no corresponding row, so a future route added to
`app.routes.ts` without a matching row entry breaks the build rather than shipping silently

**AC-215-C — a permission the session lacks shows no row**
Given a session holding only `ClientManage`
When the sidebar renders
Then only the clients row appears, and no row for any screen gated by a permission this session does
not hold is present in the DOM at all — not present-and-hidden, absent

**AC-215-D — row order is stable**
Given the same session's permission set, rendered twice, including once after a page reload
When the row lists from both renders are compared
Then the order is identical, and the ordering rule does not read object key or `Set`/`Map` iteration
order to produce it

**AC-215-E — both locales, no missing label** *(fails if the rule is broken)*
Given every row a session can produce, in both the Arabic and English locale files
When each row's i18n key is looked up in each locale
Then a translation exists in both — no key falls back to itself or renders blank in either locale

**AC-215-F — the active row is marked**
Given a session with more than one row and the router on `/catalogue`
When the sidebar renders
Then the catalogue row, and only the catalogue row, carries the active marker — including when the
current URL is a child route such as `/catalogue/new` or `/catalogue/:catalogueItemId`

**AC-215-G — the mobile drawer still works at 390px with several rows**
Given a session with at least four rows, viewport 390px wide, Arabic locale
When the drawer is opened
Then it slides in from the right (RTL inline-start), lists every row the session holds, closes on a row
tap and on the backdrop tap, and the page body does not scroll horizontally

**AC-215-H — the post-login landing is unchanged**
Given any session already covered by `landing.spec.ts`
When sign-in completes
Then the redirect target is exactly what `landingFor`/`navPathFor` decided before this story, unchanged
by the new row list existing beside it

## Definition of Ready — where this story stands

| DoR item | |
|---|---|
| Every criterion is Given / When / Then | ✅ — `AC-215-A` … `AC-215-H` |
| Stable `AC-215-<LETTER>` ids, appended never inserted | ✅ |
| Every business rule cites a source | ✅ — rules 1–10, all traced to Nabil's defect text, CLAUDE.md or `ux/navigation.md` |
| No uncited rule | ✅ |
| Permissions named explicitly | ✅ — no new permission; reads the existing evaluated set, rule 3 |
| Money behaviour named explicitly | ✅ — none |
| Arabic UI strings as i18n keys | ✅ — rule 10, `AC-215-E` |
| The audit record it writes is stated | ✅ — none, no state change |
| **QA has written at least one scenario that fails if the rule is broken** | ⛔ **Not met.** `qa/slice-2/test-cases.md`'s `TC-2` range is fully allocated to named stories and closing this story's cases means picking case numbers and Given/When/Then wording — QA's craft, not the BA's; left undone rather than guessed at, see the note below |
| Story-currency citations dated with a stable identifier | ✅ — all dated 2026-09-13 |
| Not `BLOCKED` on an open question | ⚠️ Day labour's row (rule/AC for it) is open — see Questions. Every other row in this story is unblocked and buildable now |

## Not in this story
- **Changing `landing.ts`'s post-login destination for anyone.** Rule 2 — `RULED_LANDINGS`,
  `landingFor`, `navLabelKeyFor`, `navPathFor` are untouched. If a role's landing should change, that is
  a separate ruling, not a side effect of this story.
- **Grouping rows, icons, or the Owner's row order specifically.** Not ruled by Nabil — see Questions.
  This story does not invent an answer.
- **A row for a screen with no route yet** (dashboard, treasury, approvals, projects, and everything
  else `ux/navigation.md`'s per-role tables name from slice 3 onward). `AC-215-B`'s completeness check
  runs against `app.routes.ts` as it exists today, not against the eventual navigation map.
- **A row for `/projects/:projectId/day-labour` or its worker-history child**, until the open question
  below is answered. Building a guess at the answer is exactly the thing `CLAUDE.md`'s *"if `spec.md`
  doesn't answer a business question, stop and ask"* rule exists to prevent, even though this is a UX
  question rather than a money one — the shape of the mistake is the same.
- **A separate site-shell bottom navigation.** `ux/navigation.md` names a future Site shell for
  `SiteEngineer` on mobile (slice 6+); this story's sidebar is the Staff shell only, matching every
  route it rows today.
- **Changing any guard, any permission grant, or any server-side check.** This story is presentation
  over permissions the server already evaluates and already enforces; it cannot widen or narrow what a
  session may reach.

## Questions

| # | Question | Owner |
|---|---|---|
| **new, open** | **How does a project-scoped screen get a sidebar row at all?** `/projects/:projectId/day-labour` is gated `DayLabourSiteManage`, evaluated **per project**, not on `Session.permissions` (`ux/navigation.md`: project-scoped permissions are "answered per project," never folded into the company-wide set, D-035). A company-wide sidebar row needs one fixed path; this screen needs a `:projectId` the sidebar does not have without a project already chosen. Does the row wait for a project to be selected elsewhere first, does it point at a project picker, or does it not get a top-level row at all and stay reached only from inside an open project (the same way `/babs` is reached from the catalogue list today, per `app.routes.ts`'s own comment)? **Not decided here — left open rather than guessed.** Same question applies to the worker-history route, one level deeper. | UX / Nabil |
| **new, open** | **Row grouping and icons.** Nabil's requirement says nothing about whether rows group under headings (as `ux/navigation.md`'s per-role tables suggest, e.g. "Master data") or render as a flat list, or whether rows carry icons. Left to whoever designs the visual sidebar, not decided by this story. | UX |
| **new, open** | **The Owner's row order.** Rule 6 requires the order be *stable*; it does not say what that order *is*. `ux/navigation.md`'s Owner table lists an order for the eventual full application (Dashboard, Approvals, Projects, Treasury, Clients, Master data, Users, Assignments, Audit, Accounting) that includes screens not yet routed. Whether today's partial row list should anticipate that order, or use its own until the rest exists, is not ruled. | UX / Nabil |

## Report — what this session did not do

- Did not touch `STATUS.md`, `decisions.md`, or anything under `src/` — out of scope for this BA pass
  by the brief that cut this story.
- Did not case this story in `qa/slice-2/test-cases.md`. The range is fully allocated to named stories
  with specific `TC-2-nnn` ids already assigned; adding this story's cases means choosing new ids,
  priorities and Given/When/Then wording, which is QA's judgment call, not a fact to read off the repo.
  Flagged here for QA to pick up rather than guessed at.
- Did not decide the day-labour open question, the grouping/icon question, or the Owner's eventual row
  order. All three are named above as open, not answered.
