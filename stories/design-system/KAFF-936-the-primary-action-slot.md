# KAFF-936 · The primary-action slot — one declaration, one desktop placement, one phone placement

<!-- kaff id=KAFF-936 slice=design-system points=5 state=VERIFIED verdict=CONDITIONAL at=91cdcb3 on=2026-11-17 -->

**Slice:** Design system (cross-cutting, not slice-numbered) · **Epic:** Apple-grade restyle · **Points:** 5.
**Owner:** Frontend. **This is a shell/kit story**, not a per-screen fix: it builds the contract once. It is Frontend-only and carries no backend criterion, so `process/agile.md` §2a rule 6 (D-117) does not bite.
**Source (the ruling this story discharges):** `stories/open-decisions-2026-10-08.md`, closed item 9, "The phone primary-action slot", owner "UX, then Frontend". Verbatim: *"Adopt the UX contract: a screen declares exactly one `primaryAction` and never renders a primary itself; the shell places it in the page header on desktop and the bottom bar on phone. Zero actions means the menu expands to fill. Two or more means the most frequent constructive one takes the slot and the rest go to the phone toolbar. Destructive actions are never in the bar. An open overlay hides the bar and its own footer carries the primary."*
**Also cites:** `stories/project-journey-study-2026-10-08.md`, "Design principles this study established" — *"A control the user may not use is absent, never disabled."* · `CLAUDE.md`, "Angular conventions" and "Language and terminology".
**Raised by:** a builder grepped `src/Web` for the bottom bar and slot and used a private footer instead; the daily log then copied it.

**Status: BLOCKED** on one Definition-of-Ready box: *"QA has written at least one scenario that fails if the rule is broken"* (`process/agile.md`, Definition of Ready). Also `AC-936-F` is **HELD** on `Q117`. Every other box holds: criteria are Given/When/Then with stable IDs, every rule cites a source, permissions and money are stated, and the keys are named.

## What the code looks like today

[Verified: 2026-11-01] These correct the request that asked for this story, which said there is "no bottom bar and no slot". There is a bar and a half-slot; there is no primary-action contract.

- The shell already has a phone bottom bar: `abar` in `app.html`, carrying only the menu button (`data-testid="nav-toggle"`). Its own comment says *"A screen's own primary action joins it here when that screen is redesigned."* Nothing joins it. `app.css` hides it while the nav sheet is open (`.app[data-sheet='1'] .abar`) and removes it on desktop.
- The desktop slot exists: `HeaderActionsService` (`core/layout/header-actions.service.ts`) holds one `TemplateRef`, rendered by `app.html` as the `page-actions` strip. It is a free-form template slot (search plus create), not a declared single action, and it is not rendered on phone as a bar. Six screens set it (`client-list-page.ts`, `employee-list-page.ts`, `subcontractor-list-page.ts`, `supplier-list-page.ts`, `user-list-page.ts`, `department-settings-page.ts`).
- Two screens render a private pinned footer holding `kaff-button variant="primary"`: `features/day-labour/worker-register/worker-register-page.html` (`.footer`, with a cancel link beneath) and `features/daily-log/daily-log-page.html` (`.footer`). Both footers are `position: sticky; inset-block-end: 0` with `env(safe-area-inset-bottom)` in their own CSS, duplicated. `KAFF-600` `AC-600-K` already says the daily log's primary "sits in the shell's bottom bar per the phone primary-action contract"; the code does not do that.
- Many other templates under `src/Web/src/app/features/`, plus `shared/kaff-value-editor`, contain `variant="primary"`. Most are in-page form buttons, not pinned footers.

## Business rules

| # | Rule | Source |
|---|---|---|
| 1 | A screen **declares** its primary action to the shell; it never renders a primary button of its own. | Closed item 9 |
| 2 | On desktop the shell places the declared action in the page header. | Closed item 9 |
| 3 | On phone the shell places it in the bottom bar, beside the menu button. | Closed item 9 |
| 4 | Zero declared actions: the menu expands to fill the bar. | Closed item 9 |
| 5 | Two or more declared: the most frequent constructive one takes the slot; the rest go to the phone toolbar. | Closed item 9 — **HELD on `Q117`** |
| 6 | A destructive action is never in the bar. | Closed item 9 |
| 7 | An open overlay hides the bar; the overlay's own footer carries the primary. | Closed item 9 |
| 8 | An action the user may not use is absent from the slot, never disabled and never greyed. | Journey study, "Design principles" |
| 9 | Logical properties only; the layout is correct RTL at 390px. Controls in the phone bottom bar are 56 to 60px. | `CLAUDE.md` "Angular conventions" · `.design/v2/team-a/README.md`, the touch-target rule |
| 10 | No hardcoded user-facing string; every key in both `ar.json` and `en.json` and read by a template. | `CLAUDE.md` "Language and terminology" |
| 11 | The slot is built standalone, with `inject()` and signals, zoneless, new control flow. | `CLAUDE.md` "Angular conventions" |

**Money:** none. **Audit:** none written — no state changes; this is presentation. **Permissions:** none new. The slot does not decide who may act; the declaring screen passes in what the session may do, and server-side checks remain the gate (`CLAUDE.md`, "Never enforce permissions in the frontend alone").

## Acceptance criteria

**AC-936-A — one declaration mechanism**
Given a screen that wants a primary action
When it declares one through the shell's single `primaryAction` mechanism
Then the shell renders it, and the screen's own template contains no `kaff-button` of `variant="primary"` for that action
*Rule: 1. Closed item 9.*

**AC-936-B — desktop placement**
Given a screen that has declared an action, at desktop width
When the page renders
Then the action appears in the page header, once, and not in the bottom bar (which is not rendered on desktop)
*Rule: 2. Closed item 9.*

**AC-936-C — phone placement**
Given the same screen at 390px
When the page renders
Then the action appears in the shell's bottom bar (`abar`) beside the menu button, 56 to 60px high, clear of the safe-area inset, and not in the page header
*Rules: 3, 9. Closed item 9.*

**AC-936-D — zero actions**
Given a screen that declares no action, at 390px
When the page renders
Then the menu button expands to fill the bar and no empty slot or placeholder is drawn
*Rule: 4. Closed item 9.*

**AC-936-E — absent, never disabled**
Given a session that may not use a screen's action
When the screen renders
Then the action is not in the DOM at all in the header or the bar, and the bar behaves as `AC-936-D`; there is no `disabled` or greyed variant of the slot
*Rule: 8. Journey study, "A control the user may not use is absent, never disabled".*

**AC-936-F — two or more declared — HELD**
Given a screen that declares two or more actions
When the shell resolves them
Then the "most frequent constructive" one takes the slot and the rest go to the phone toolbar
*Rule: 5. **HELD on `Q117`**: no document says how frequency is known, and the only phone toolbar defined (`.design/v2/team-a/README.md`, "Phone toolbar") is the catalogue list's search-and-filter block. Do not build this criterion until `Q117` is answered.*

**AC-936-G — a destructive action cannot take the slot**
Given a screen that declares a destructive action (`variant` of `danger` or `danger-outline`) as its primary
When the declaration is made
Then it is refused loudly: a compile-time type error where the type can express it, and a thrown error at runtime otherwise — never silently rendered in the bar
*Rule: 6. Closed item 9; `.design/v2/team-a/README.md`, "Destructive or irreversible actions are never in the phone bottom bar".*

**AC-936-H — an open overlay hides the bar**
Given a declared action and an open overlay (the nav sheet, `kaff-confirm-dialog`, `kaff-detail-panel`)
When the overlay is open
Then the bar is hidden, and the overlay's own footer carries the primary
*Rule: 7. Closed item 9. Today only the nav sheet hides the bar.*

**AC-936-I — the contract can be violated loudly**
Given a screen template under `src/Web/src/app/features/` that renders `kaff-button variant="primary"` for a screen-level action instead of declaring it
When the frontend unit suite runs
Then a test fails, naming the template, unless that template is on an explicit allowlist of screens not yet migrated; the allowlist may only shrink, and a test fails if an allowlisted template no longer renders a primary
*Rule: 1. This criterion keeps the contract from being a check that reports a safety it does not have. The allowlist is a delivery mechanism so the un-migrated templates do not turn the gate red on day one; it is not a business rule. Whether those templates should migrate is named under "Not in this story".*

**AC-936-J — the two improvising screens move onto the contract**
Given `worker-register-page` and `daily-log-page`
When this story is done
Then each declares its primary action through the shell mechanism, its private `.footer` markup and the `.footer` rules in its CSS are deleted, neither is on the `AC-936-I` allowlist, and the `data-testid` values `worker-submit` and `daily-log-save` still reach the rendered button
*Rules: 1, 3. Closed item 9; `KAFF-600` `AC-600-K`. Worker-register's cancel link stays on the screen as a plain link: it is not a primary.*

**AC-936-K — Arabic RTL at mobile width, logical properties only**
Given the bar and the header slot in Arabic at 390px
When the page renders
Then the layout is correct RTL, there is no horizontal overflow, and the new CSS uses only logical properties (`margin-inline-start`, `inset-block-end`) — no `margin-left`, `left`, `right`
*Rule: 9. `CLAUDE.md`, "RTL is the primary direction, not a mirror".*

**AC-936-L — every string is a key, and every key is read**
Given any text the slot or bar adds (including the accessible name of a declared action)
When the translation gate runs
Then each key exists in both `src/Web/public/locales/ar.json` and `en.json`, and a template reads each one; a key no template reads fails the Domain translation gate
*Rule: 10. `CLAUDE.md`, "No hardcoded user-facing strings". If the story adds no new string, say so in the commit.*

## Not in this story

- The other templates that render `variant="primary"` and the six screens that set `HeaderActionsService`. Moving them is a separate story; this one lands the contract and migrates only the two named in `AC-936-J`. Whether `HeaderActionsService` becomes the declaration mechanism, wraps it, or is retired is the builder's call, but there must be **one** mechanism at the end, not two.
- Any change to what a screen's primary action does, or its permission check.
- Resolving two-or-more actions (`AC-936-F`, `Q117`).
- Any backend change.

## For QA (the box this story fails)

Scenarios that must fail if the rule breaks: a screen declaring a destructive primary (`AC-936-G`); a template re-adding a private primary footer (`AC-936-I`); the bar still showing under an open `kaff-confirm-dialog` (`AC-936-H`); an unauthorised session's action present but disabled (`AC-936-E`). QA has not written these yet.
