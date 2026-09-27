# Flow simplicity — how many steps the work costs today, and what it could cost

**Date:** 2026-09-26 · **Author:** UX research 3 of 3, angle *flow simplicity — fewest steps for the
person doing the work* · **Read-only study.** No source file was edited by this pass.

**Why this exists.** Nabil, 2026-09-26: *"i think the ux is a little bit complicated … see the best
flow to make it easier … i am very disapointed."* The demo is 2026-09-27.

**Scope limit taken seriously.** Nothing below proposes a rule `spec.md` does not already permit.
Where a business question is unanswered it is listed in §7 as a question for Nabil or Karim and **no
choice is made here**. Two rules were treated as hard walls, per `CLAUDE.md`: money never moves
offline (offline creates drafts only), and nobody creates and approves the same movement. A shorter
flow that breaks either is a defect, not a simplification.

---

## 1. How a step is counted

One **step** is one action the person must take, or one screen they must wait for, between submitting
their credentials and the act being committed:

- a click or tap on a link, row, menu entry or button — **1**
- a screen the router renders on the way, even if nothing is done on it — **1**
- typing into fields on a screen already open — **0** (typing is the work, not ceremony)
- submitting a form — **1**

Signing in itself is step 1 in every count, so all roles start from the same baseline.

---

## 2. The finding that dominates every count

**Five of the seven staff roles have no navigable path to a project, and four of the seven land on a
screen they cannot act on.** This is not a styling problem and it is not a backend problem any more.

- The sidebar carries exactly nine rows, all company-wide master data
  (`src/Web/src/app/core/navigation/nav-rows.ts:35-45`). There is no project row, no treasury row and
  no home row.
- Day labour — the only built project-scoped feature — is deliberately absent from that table because
  the route needs a `:projectId` a company-wide row cannot supply
  (`src/Web/src/app/core/navigation/nav-rows.ts:29-33`, and the routes at
  `src/Web/src/app/app.routes.ts:236-252` and `:258`).
- The one screen that *does* list a person's projects renders them as plain `<span>`s, not links
  (`src/Web/src/app/features/landing/landing-page.html:44-52`). HR's list is the same
  (`:66-80`).

**The data needed to fix this is already on the wire.** `GET /api/auth/me` returns, per project, both
the `projectId` and the caller's evaluated project-scoped permission list
(`src/Web/src/app/core/auth/auth.service.ts:43-50`, produced by
`src/Api/Features/Auth/WhoAmI/Handler.cs:170`). So a link from a project row to
`/projects/:projectId/day-labour`, gated on that row's own `DayLabourSiteManage`, needs **no backend
change and no new permission**. `nav-rows.ts:29-33` says the question of how day labour gets a row is
unanswered; the payload answers it.

**Two more APIs that shipped after the 2026-09-21 design was written make its §6 table stale.**
That design recorded *"No `Features/Projects/` exists"* and *"`Features/Treasury/` … nothing reads"*
(`docs/superpowers/specs/2026-09-21-shell-and-interaction-redesign-design.md:119-127`). Both are now
false:

| Endpoint | Permission | Scope | Granted to |
|---|---|---|---|
| `GET /api/projects` (`src/Api/Features/Projects/ListProjects/Endpoint.cs:45-46`) | `ProjectList` | CompanyWide | Owner, Finance, TechnicalOffice, Marketing, SiteEngineer (`src/Domain/Authorization/PermissionCatalogue.cs:542-544`) |
| `GET /api/projects/{id}` (`src/Api/Features/Projects/GetProject/Endpoint.cs:37-38`) | `ProjectRead` | ProjectScoped | — |
| posting list, company and project (`src/Api/Features/Treasury/ListPostings/Endpoint.cs:40-65`) | `TreasuryReadCompany` / `TreasuryReadProject` | both | Owner, Finance (+ SiteEngineer on the project row) (`PermissionCatalogue.cs:498-506`) |
| derived balances (`src/Api/Features/Treasury/GetBalances/Endpoint.cs:33-45`) | same | both | same |

`ProjectList` being **company-wide** is the important detail: one row added to
`nav-rows.ts:35-45` puts a project list in the sidebar of five roles with zero backend work, zero
permission change, and no new rule. `TreasuryReadCompany` does the same for Finance and the Owner.
That single row is the largest step cut available before tomorrow.

---

## 3. Steps per role, today

Roles are `spec.md` §9's, as amended: Owner · Finance/Accounts · Technical Office · Site Engineer ·
Head of Design (phase 2) · Marketing/Sales · HR. **There is no Storekeeper role** — `spec.md` §9:367
does not list one, and the only store reference in the whole specification is assumption 7 in §16
("one warehouse, cost charged on issue", owner: Karim). See §7, question 5.

`∞` below means *no path exists through the user interface*; the only way in is to type a URL, and in
the Site Engineer's case the URL contains a GUID no screen ever renders.

| Role | The thing it does most (`spec.md`) | Steps today | Screens crossed |
|---|---|---:|---:|
| Owner | approve every financial movement (§7, §9 "Owner approves all financial movements") | **∞** | — |
| Owner | read the safe / bank balance (§6.1, §6.4) | **∞** | — |
| Owner | create a user (§9 amendment 5) | 5 | 3 |
| Finance | post, collect, disburse, read a ledger (§6) | **∞** | — |
| Finance | edit a supplier (§2 — the only master record Finance owns) | 6 | 3 |
| Technical Office | the extract quantity gate (§7, BLOCKING) | **∞** | — |
| Technical Office | fix a catalogue price (§4.1, §4.2) | 5 | 2 |
| Technical Office | edit any other catalogue field | 7 | 3 |
| Site Engineer | the daily log (§8 — "the reference mobile screen for the whole product") | not built (slice 6) | — |
| Site Engineer | register a worker from site (§10) | **∞** | — |
| Site Engineer | engage a worker on today's job (§10) | **∞** | — |
| HR | maintain the employee register (§10 — HR is the single source) | 7 | 4 |
| Marketing/Sales | register a client (§2, §3) | 5 | 3 |
| Marketing/Sales | find and correct a client (§3) | 6 | 3 |
| Head of Design | — (phase 2, `spec.md` §9:367) | n/a | — |

### 3.1 Where each count comes from

**Owner — create a user, 5 steps.**
1 submit sign-in (`src/Web/src/app/features/auth/sign-in/sign-in-page.ts:146` navigates to `/`) ·
2 the landing page renders a redirect frame (`src/Web/src/app/features/landing/landing-page.html:87-91`) ·
3 the router settles on `/users` (`src/Web/src/app/core/navigation/landing.ts:56-58`) ·
4 the primary create action in the top bar · 5 submit. **This one is fine.** It is the only role whose
built path matches its landing.

**Owner — approve a movement, ∞.** There is no Extracts slice at all
(`docs/superpowers/specs/2026-09-21-shell-and-interaction-redesign-design.md:126`), and the treasury
posting and reversal endpoints (`src/Api/Features/Treasury/PostMovement/`, `ReversePosting/`) have no
screen and no route in `src/Web/src/app/app.routes.ts`. This is the Owner's stated primary duty and
it is unreachable. It is backend/story work, not a flow fix, and is named here so the step table is
honest rather than flattering.

**Finance — edit a supplier, 6 steps.**
1 sign-in · 2 `/` · 3 the landing falls through every ruled row and renders **My profile**
(`src/Web/src/app/core/navigation/landing.ts:121-124`) · 4 the suppliers link
(`src/Web/src/app/features/landing/landing-page.html:35-39`, or the identical sidebar row at
`nav-rows.ts:40`) · 5 row menu → edit, which navigates to a full form page
(`src/Web/src/app/features/suppliers/supplier-list/supplier-list-page.ts:7,30`) · 6 submit.

**Technical Office — fix a catalogue price, 5 steps.**
1 sign-in · 2 `/` · 3 `/catalogue` (`landing.ts:62`) · 4 click the row, which opens a detail panel
(`src/Web/src/app/features/catalogue/catalogue-list/catalogue-list-page.html:203-250`) · 5 commit the
inline edit (`:221-228`). **This is the best flow in the product** and is what `KAFF-931` bought.
Every other list should look like it. Editing any field the panel does not carry costs 2 more: the
"تعديل" link at `:239-249` navigates to the full form page, then submit.

**Site Engineer — register or engage a worker, ∞.**
1 sign-in · 2 `/` · 3 **My profile** (`landing.ts:121-124` — the Site Engineer holds no company-wide
permission in `RULED_LANDINGS`) · and then nothing. The profile lists the engineer's projects
(`landing-page.html:44-52`) as text; `/projects/:projectId/day-labour` needs the GUID, which that
screen has in memory (`auth.service.ts:44`) and never renders. **The engineer's entire feature set —
worker pool, register-from-site, engage, close, rate — is built, shipped and unreachable.** Once
inside, the screens themselves are good: registration is one screen with four fields
(`src/Web/src/app/features/day-labour/worker-register/worker-register-page.html`), and engage / close
/ rate are inline on the pool row with no confirm and no navigation
(`src/Web/src/app/features/day-labour/worker-pool/worker-pool-page.html:60-113`). The flow is right;
the way in does not exist.

**HR — maintain the employee register, 7 steps.**
1 sign-in · 2 `/` · 3 **HR project list** (`landing.ts:117-119`) — a list whose rows are deliberately
not clickable (`landing-page.html:59-62`, `:66-80`) · 4 the sidebar "الموظفون" row (`nav-rows.ts:38`)
· 5 row menu (`src/Web/src/app/features/employees/employee-list/employee-list-page.ts:170-176`) ·
6 the full employee form page (`:178-187` navigates) · 7 submit. Step 3 is a screen HR must pass
through and cannot use.

**Marketing/Sales — find and correct a client, 6 steps.**
1 sign-in · 2 `/` · 3 `/clients` (`landing.ts:60`) · 4 the row, a stretched link to the full form page
(`src/Web/src/app/features/clients/client-list/client-list-page.html:64-69`) · 5 edit · 6 submit.
No detail panel, no inline edit, no row menu on this list at all.

---

## 4. The ceremony

### 4.1 The `/` hop — one wasted screen for every sign-in

`sign-in-page.ts:146` navigates to `/`; the landing component then redirects again for the three roles
that have a ruled destination, rendering a "loading" frame in between
(`landing-page.html:87-103`). `navPathFor(session)` already computes the destination
(`src/Web/src/app/core/navigation/landing.ts:166-177`). Signing in straight to it removes one screen
from every session of every role. **Cut. −1 step, every role, every day.**

### 4.2 Archive asks twice, and the ruling says it should not ask at all

Archiving is soft and reversible (`KAFF-214` un-archives a catalogue item). Four screens still make it
a two-click confirmation:

| Screen | Confirm |
|---|---|
| catalogue list | `catalogue-list-page.html:165-184` |
| باب tree | `src/Web/src/app/features/babs/bab-tree/bab-tree-page.html:142-161` |
| employee list | `employee-list-page.ts:189-213` |
| client form | `src/Web/src/app/features/clients/client-form/client-form-page.html:187-206` |

The approved design already rules the other way: *"Archive executes immediately with an 8-second undo
and no confirm"*
(`docs/superpowers/specs/2026-09-21-shell-and-interaction-redesign-design.md:103-106`). The component
that would carry it, `kaff-undo-toast`, is one of the six that design names (`:113-115`) and is one of
the two never built — `src/Web/src/app/shared/` holds `kaff-detail-panel`, `kaff-inline-edit`,
`kaff-row-menu` and the state set, but no `kaff-undo-toast` and no `kaff-bulk-bar`. The confirm is a
stand-in for a component nobody built. **−1 step per archive, on four screens, once the toast exists.**

### 4.3 Archiving a client costs a whole page the catalogue does not charge

The client list has no row menu (`client-list-page.html:57-94`); the only archive control is in a
danger section at the bottom of the full form page (`client-form-page.html:187-206`). Catalogue and
employees do it from the row. Same for users. **Cut: put `kaff-row-menu` on the client, user and
supplier lists, exactly as `catalogue-list-page.html:139-146` has it. −2 steps per archive.**

### 4.4 Two file pickers on one screen

`src/Web/src/app/features/catalogue/catalogue-import/catalogue-import-page.html:33` is the first-import
picker; `:100-110` is a second, separate re-import picker with its own preview and confirm
(`:118-197`). The person has to know which of the two flows they are in before they can choose a file.
`KAFF-201`'s confirm re-parses statelessly, so the server already distinguishes the cases. **One
picker; keep the preview-and-confirm — repricing a live catalogue earns it.**

### 4.5 A duplicated destination

Finance reaches suppliers from the profile link (`landing-page.html:35-39`) *and* from the sidebar row
(`nav-rows.ts:40`). The profile link predates the sidebar row. **Cut the profile link.**

### 4.6 `window.confirm` on six routes

`confirmUnsavedChangesGuard` is wired to six form routes (`src/Web/src/app/app.routes.ts:121, 142,
166, 176, 212, 223`) and is a native `window.confirm`
(`src/Web/src/app/core/navigation/unsaved-changes.guard.ts:15`). In an Arabic RTL product this is
browser chrome in the browser's language, with the OK/Cancel order the OS chooses. Not a step-count
problem; a consistency problem, and it is the only untranslated string a user can see. Named, not
scheduled.

### 4.7 Ceremony that should stay

- **Re-import preview and confirm** (`catalogue-import-page.html:118-197`). It reprices live
  catalogue rows. Keep.
- **Role change and deactivation confirms** (`src/Web/src/app/features/users/user-form/user-form-page.html:287-330`
  and `:333-391`). A role change revokes every project assignment (`spec.md` §9, D-051), and the
  confirm names the projects before the act. That is exactly what a confirmation is for. Keep.
- **Reactivation confirm** (`:393-445`). Restoring a login is a security act. Keep.
- **The duplicate-phone warning** (`worker-register-page.html:36-53`, and the shared
  `shared/duplicate-phone-warning/`). D-049 ruling 8 requires it and requires that *proceed* be a real
  outcome. Keep exactly as is.

### 4.8 Where it does **not** ask and money is involved

Two inline commits write money-shaped values with no confirmation and no visible correction path:

1. **A catalogue item's cost price**, committed optimistically from the detail panel
   (`catalogue-list-page.html:221-228`).
2. **A باب's default markup**, committed optimistically from the tree row
   (`bab-tree-page.html:63-70`).

Both feed `lineRate = baseSellRate × (1 + Σ conditions) × (1 + lineMarkup)` (`spec.md` §4.2), and §4.4
freezes those values into a signed BOQ by copy. The approved design says *"Never inline: anything that
changes what money is legal, meaning posting amounts, extract quantities, hold percentages"*
(`…redesign-design.md:108-109`) — a catalogue price is not on that list and is not obviously off it.
**This is a business question, not a UX preference, and it is not decided here.** See §7, question 1.

3. **The day rate on a worker engagement** (`src/Web/src/app/features/day-labour/worker-history/worker-history-page.html:56-76`).
   This one is sharper: the editor renders only while `engagement.dayRate === null` (`:52-56`), so the
   value is write-once and the screen offers no way to correct it afterwards. It is gated on
   `DayLabourRateManage`, which D-153 §1 created precisely because the day rate is money, and it
   multiplies into payroll (`spec.md` §10). A one-shot, uncorrectable money entry with no confirmation
   is the shape of an expensive mistake. See §7, question 2.

None of this touches the append-only ledger rules; no screen in the SPA writes a posting today.

---

## 5. What comparable products do

Cited because two of these contradict how this application is currently shaped.

1. **Field reporting is two or three taps, or it does not get used.** hh2 states progress notes and
   photos are added "in two taps", and that the bar is a foreman using it after a twelve-hour shift.
   Banamind puts the same number on it: a site reporting app "needs to work in two or three taps,
   store data offline, and push a structured report the moment connectivity returns." Kaff's
   worker-registration screen already meets this bar *once reached*; the cost is entirely in reaching
   it. — https://www.hh2.com/construction-management/how-to-improve-daily-field-reporting-in-construction
   · https://banamind.ai/construction-reports/construction-site-reporting-app
2. **Crew hours are logged "in a few taps" straight into costing**, not through a navigate-to-form
   cycle — the pattern Kaff's worker pool already follows with inline engage/close/rate. —
   https://www.miter.com/field-operations/daily-reports/
3. **Offline is drafts plus queued sync, not offline money.** Procore queues cached mobile actions for
   later sync; Fieldwire stores task changes locally until service returns. Both keep the offline
   artefact a draft. This matches `CLAUDE.md` and `spec.md` §8 exactly and is worth citing back at
   anyone who proposes shortening a flow by settling money on the phone. —
   https://www.procore.com/quality-safety/daily-log ·
   https://ablemkr.com/offline-construction-app-what-works-without-signal/
4. **Inline edit versus navigate-to-form.** "Inline editing allows users to click on a cell and start
   editing without an additional step… there is no chance of losing the context since no navigation is
   involved," while moving the user to a separate view means they "lose the context of their work and
   data." The same sources note the counter-case that matters here: for high-stakes data, deliberate
   friction — an expandable row or a panel rather than a raw cell — "leaves less room for error." That
   is the argument for Kaff's detail panel as the default, with the full page reserved for money. —
   https://uxdworld.com/inline-editing-in-tables-design/ ·
   https://www.pencilandpaper.io/articles/ux-pattern-analysis-enterprise-data-tables
5. **The approval inbox is one screen you can act from.** Oracle's Inbox pattern is "a single screen
   that shows all approval and notification items assigned to them regardless of the screen where the
   item originated, with enough detailed information about each item so users can act on it from the
   Inbox without navigating to other screens." Note the qualifier — the 2026-09-21 design's own rule 6
   ("a universal inbox that sends you elsewhere to act just creates another place to check",
   `…redesign-design.md:61-62`) is the same point, and both are satisfied only if the approve/reject
   action lives in the queue row. —
   https://docs.oracle.com/cd/E95904_01/books/AppsAdmin/administering-and-using-inbox.html
6. **Approvers need the context in the row.** "Reviewers can't make fast decisions if the submission
   arrives without context." For Kaff that means the extract queue row carries work value, hold and
   net payable — the `spec.md` §5.1 display set — not just a reference number. —
   https://www.teamwork.com/blog/approval-workflow-best-practices/

---

## 6. The simplest flow per role

Everything below is buildable against endpoints that exist today, changes no permission, and invents
no business rule. Steps are counted on the same convention as §1.

### 6.0 Two changes that pay for six of the seven roles

**(a) Sign in to the destination, not to `/`.** `sign-in-page.ts:146` calls
`navPathFor(session)` (`landing.ts:166-177`) instead of `'/'`. **−1 step for every role.**

**(b) Add two rows to `nav-rows.ts:35-45`.**

```
{ permission: 'ProjectList',          path: '/projects',  labelKey: 'nav.projects',  group: 'core'  }
{ permission: 'TreasuryReadCompany',  path: '/treasury',  labelKey: 'nav.treasury',  group: 'core'  }
```

Both permissions are `CompanyWide` and already on `Session.permissions`
(`PermissionCatalogue.cs:504-506`, `:542-544`), so the existing filter at `nav-rows.ts:52-54` needs no
change and the server still decides every request. `/projects` needs one list screen over
`GET /api/projects`; `/treasury` needs one screen over `GET` balances. **This is what turns `∞` into a
number for Owner, Finance, Technical Office and Site Engineer.**

### 6.1 Site Engineer — engage a worker on today's job

| | Before | After |
|---|---|---|
| Steps | **∞** | **3** |

1. Submit sign-in → the engineer's project opens directly when they hold exactly one project carrying
   `DayLabourSiteManage` (both facts are already in `auth.service.ts:43-50`); with more than one, sign
   in to the project list and pick one — **step 2**.
2. The worker pool is the route's index (`app.routes.ts:239-243`) — it renders on arrival.
3. Press "تشغيل" on the worker's row — inline, already built
   (`worker-pool-page.html:104-112`), no navigation and no confirm.

Register a new worker instead: **5** (…, "تسجيل عامل" `worker-pool-page.html:4-6`, submit) against
**∞** today.

Nothing here moves money and nothing here approves anything, so both hard rules hold. The day rate
stays off every Site Engineer route exactly as `Q76`/D-153 §1 leave it.

### 6.2 Finance — read a balance, then act

| | Before | After |
|---|---|---|
| Read the safe / a project ledger | **∞** | **2** |
| Edit a supplier | 6 | **4** |

1. Submit sign-in → lands on `/treasury` (Finance's first ruled landing that is actually Finance's
   job; `TreasuryReadCompany` is Owner-and-Finance, so put it *after* `UserManage` in
   `RULED_LANDINGS` at `landing.ts:56-63` and the Owner's landing does not move — the same precedence
   trick that file already documents at `:41-45`).
2. The balances render. Drill into a posting list from a figure — the design's rule 5
   (`…redesign-design.md:59-60`) — is step 3, and `ListPostings` already serves it.

Supplier edit drops to 4 by giving the supplier list the catalogue's detail panel instead of a form
page. Delete the duplicate profile link (§4.5).

**Not proposed:** any screen that posts or disburses. `PostMovement` exists, but a Finance-facing
posting screen has to answer who approves it, and `spec.md` §9 forbids the creator approving. That is
a story, not a flow tweak.

### 6.3 HR — maintain the employee register

| | Before | After |
|---|---|---|
| Steps | 7 | **4** |

1. Submit sign-in → lands on `/employees`. HR holds `EmployeeManage`, which is company-wide and
   already on the payload, so this is one row appended to `RULED_LANDINGS` (`landing.ts:56-63`) after
   the three that exist. It also retires the special-cased HR branch at `landing.ts:117-119`, which
   that file's own comment calls a hole in the payload rather than a landing.
2. The list renders; search or filter in place.
3. Click the row → detail panel (the catalogue's, reused).
4. Commit the change inline, or follow the panel's edit link for the fields it does not carry.

The HR project list is not deleted — it becomes a sidebar row, where a screen nobody can act on costs
nothing to ignore. Its rows stay unclickable until D-103's routing question is answered; that is not
this document's to decide.

### 6.4 Technical Office — the catalogue

| | Before | After |
|---|---|---|
| Fix a price | 5 | **4** |
| Edit another field | 7 | **5** |

Only §6.0(a) is needed for the first row. The second comes from carrying the remaining editable
fields in the detail panel rather than bouncing to `/catalogue/:id`. The quantity gate — the Technical
Office's actual daily duty under `spec.md` §7 — stays `∞` until an Extracts slice exists, and no
navigation change hides that.

### 6.5 Marketing/Sales — the client master

| | Before | After |
|---|---|---|
| Register a client | 5 | **4** |
| Find and correct a client | 6 | **4** |
| Archive a client | 5 | **3** |

§6.0(a) removes one step from all three; a row menu and a detail panel on the client list
(§4.3) remove the form-page round trip from the second and third. The duplicate-phone warning stays
in the create path untouched — it is the one interruption `spec.md` requires.

### 6.6 Owner

| | Before | After |
|---|---|---|
| Create a user | 5 | **4** |
| Read the safe | **∞** | **3** |
| Open a project | **∞** | **3** |
| Approve a movement | **∞** | **∞** — needs the Extracts slice |

The Owner's landing stays `/users` under §6.0(b)'s precedence note. Whether it *should* is §7
question 3: `ux/navigation.md` rules an Owner dashboard (S-037, `ux/screen-inventory.md:83`) and
`spec.md` §6.4.3 requires aggregate firm-advance exposure on it. Neither exists, and inventing a tile
board is what `ux/navigation.md` explicitly forbids.

### 6.7 Head of Design

Phase 2 (`spec.md` §9:367). Lands on My profile, which is honest. No change proposed.

---

## 7. Questions — for Nabil or Karim. Not decided here.

1. **Is a catalogue cost price or a باب default markup "money" for the purposes of the inline-edit
   ban?** The approved design forbids inline editing of "anything that changes what money is legal"
   and lists posting amounts, extract quantities and hold percentages
   (`…redesign-design.md:108-109`). Both values are edited inline today
   (`catalogue-list-page.html:221-228`, `bab-tree-page.html:63-70`) and both feed `spec.md` §4.2's
   `lineRate`, which §4.4 then freezes into signed contracts by copy. Confirm before commit, or leave
   inline? **Nabil / Architect.**
2. **May an engagement's day rate be corrected after it is set?** The screen writes it once and then
   renders it read-only (`worker-history-page.html:52-56`), with no reversal path. `spec.md` §10 makes
   it a payroll input. If a typo is unrecoverable, this needs either a correction route or a
   confirmation step, and which one is a business call. **Karim, via Nabil.**
3. **What is the Owner's landing?** `ux/screen-inventory.md:83` rules S-037, and `spec.md` §6.4.3
   requires aggregate firm-advance exposure on it. Until the firm-advance ledger exists there is
   nothing true to put there. Land the Owner on the project list in the meantime, or leave them on the
   user list? **Nabil.**
4. **Should a Site Engineer with exactly one assigned project sign in straight into that project?**
   It is the difference between three steps and four, every morning. It is also a behaviour change
   nobody has asked for, and it makes the second project someone is assigned to feel like a
   regression. **Nabil.**
5. **Is there a Storekeeper?** `spec.md` §9:367 lists eight roles plus HR and names no store role; the
   only store reference is assumption 7 in §16, owned by Karim. If Kaff has a storekeeper who will use
   this system, that is a new role with a new permission set and it is not in any slice. **Karim.**
6. **Does the 8-second undo satisfy Kaff for archiving a client or an employee**, replacing the
   confirmation (`…redesign-design.md:103-106`)? The design rules it; nobody has confirmed it against
   a real operator who archives the wrong row and walks away from the screen. **Nabil.**

---

## 8. Summary of step counts

| Role | Act | Before | After | Where the cut comes from |
|---|---|---:|---:|---|
| Site Engineer | engage a worker | ∞ | 3 | §6.0(b), linkable project rows |
| Site Engineer | register a worker | ∞ | 5 | same |
| Finance | read a balance | ∞ | 2 | §6.0(b), one treasury screen |
| Finance | edit a supplier | 6 | 4 | §6.0(a), detail panel, §4.5 |
| Owner | read the safe | ∞ | 3 | §6.0(b) |
| Owner | open a project | ∞ | 3 | §6.0(b) |
| Owner | create a user | 5 | 4 | §6.0(a) |
| HR | edit an employee | 7 | 4 | landing on `/employees`, detail panel |
| Technical Office | fix a price | 5 | 4 | §6.0(a) |
| Technical Office | edit an item | 7 | 5 | §6.0(a), fuller panel |
| Marketing/Sales | correct a client | 6 | 4 | §6.0(a), row menu + panel |
| Marketing/Sales | archive a client | 5 | 3 | §4.3, §4.2 |
| Owner | approve a movement | ∞ | ∞ | needs the Extracts slice — not a UX fix |
| Technical Office | the quantity gate | ∞ | ∞ | same |

**The one-sentence version.** The screens that exist are mostly good; four of seven roles land on a
screen they cannot act on, and every project-scoped screen in the product is unreachable because no
navigation surface carries a project — which two rows in `nav-rows.ts` and one line in
`sign-in-page.ts` would fix against APIs that already ship.
