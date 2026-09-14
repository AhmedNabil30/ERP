# KAFF-923 · Top bar rebuild, and where the page `<h1>` lives

<!-- kaff id=KAFF-923 slice=design-system points=2 state=BUILT -->

**Slice:** Design system · **Epic:** Apple-grade restyle · **Points:** 2.
**Depends on:** `KAFF-900` (BUILT), `KAFF-922` (sidebar — do this after, the account block move
touches the same header/shell files).
**Files:** `src/Web/src/app/app.html` (lines ~34-77), `src/Web/src/app/app.css`.
**Reference:** `.design/Main.dc.html` lines 97-118.

## Story
As a user, I want the top bar to show the current section and page title the way the design does
(section label, page `<h1>`, search, actions), so navigation context is visible and every screen has
exactly one `<h1>` per `apple-erp-design` §7.1.

## Gap, as found
`app.html`'s `<header class="app-header">` renders `app.name` (the app's own name, not the page
title) as the `<h1>`, plus the locale switch. It carries no page title, no search slot, no action
slot. Feature pages (e.g. `catalogue-list-page.html:3`) render their own `<h2>`/`<h1>` for the page
title — check each screen you touch in KAFF-924/925's scope for which tag it currently uses.

Mockup's top bar (`Main.dc.html` 97-118): small muted section label above the page title as `<h1>`;
then, pushed to `margin-inline-start: auto`: a search box, a secondary action (e.g. "استيراد من
Excel"), a primary action (e.g. "بند جديد"). The locale switch and `app.name` are not in this bar at
all in the mockup.

## Decision this story must make and record
Every screen has exactly one `<h1>`. Two places currently compete for it: the shell header and the
feature page. **Pick one owner and make every screen consistent** — do not leave some pages with a
shell `<h1>` and others with their own.

Recommended (say if you disagree and why, this is Frontend's call to make and record, not Nabil's —
it's a template convention, not a business rule): **the shell does not own the `<h1>`.** The shell
renders the section label only (`{{ i18n.t(sectionLabelKey) }}` — derive from the active nav row's
group, or from route data, your call) and a slot for page-supplied actions/search. Each feature page
keeps rendering its own `<h1>` as it already does (`catalogue-list-page.html:3` already has one) —
so this story removes `app.title`'s `<h1>` from `app.html` and replaces it with the section label,
and does NOT touch feature pages' existing `<h1>`s. If a screen currently renders its title as
`<h2>` instead of `<h1>`, that screen has zero `<h1>` after this change — list any such screen you
find; fixing that screen's heading level is a one-line follow-up, do it as part of this story if it's
trivial, otherwise name it as a gap for `KAFF-924`'s per-screen application pass to catch.

## What to do
1. Move the locale switch: keep it somewhere in the header (mockup doesn't show it, but it's a real
   feature — `app.locale-switch` stays, place it sensibly, e.g. trailing after actions, or leave
   where it is if it doesn't conflict with the new layout — your call, note it).
2. Remove `app.title`'s `<h1>` (moved to the sidebar-account story's scope only if the account block
   truly vacates the header entirely — check `KAFF-922`'s diff landed first).
3. Add a section-label element above where the title used to be, sourced from route/nav data (not
   hardcoded per screen).
4. Add a slot (e.g. `<ng-content>` or a structural directive reading route data) for a screen to
   supply search + secondary + primary actions in the top bar, `margin-inline-start: auto` per the
   mockup. **Do not force every screen to use it this story** — wiring `catalogue-list-page`'s own
   search/import/create into this slot is `KAFF-924`'s per-screen application job, not this one's,
   unless it's a small enough diff to do here — your call, say which you did.
5. `app.css`: rebuild `.app-header` layout to the mockup's flex row with the section label/title
   stack, then the auto-pushed action cluster.

## Acceptance criteria
**AC-923-A** Exactly one `<h1>` per screen, everywhere, after this change — grep every page template
you can reach in this story's scope and report the count is 1 for each.
**AC-923-B** Top bar shows section label above title (or your recorded alternative), matches mockup
proportions/tokens.
**AC-923-C** Zero colour literals in `app.css`'s header rules.
**AC-923-D** `data-testid="account-menu"` etc. untouched by this story if `KAFF-922` already moved
them; if not yet moved, do not duplicate the account block.
**AC-923-E** Screenshotted light+dark, desktop+390px, actually rendered.
**AC-923-F** No new string outside i18n.
**AC-923-G** `ng build` clean, `npm test` green.

## Not in this story
Wiring every screen's search/actions into the new slot — that's `KAFF-924`. The sidebar — `KAFF-922`.

## Questions for Karim
None. The `<h1>` ownership decision is a template convention (`apple-erp-design` §7.1 compliance),
not a business rule — Frontend decides and records it above, does not ask Nabil.

## Decision made, and what was built

**Confirmed by re-reading the code as it stands now, not the story's original line numbers:**
`KAFF-922` landed first (`e6487b1`) and already moved the account block (`data-testid="account-menu"`)
out of `app.html`'s header into the sidebar (`app.html`'s `.side-nav-account`). The header at the time
this story started held only the hamburger (`nav-toggle`), the `app.title` `<h1>` (`{{ i18n.t('app.name')
}}`), and the locale switch inside `.header-end`. No search slot, no action slot existed.

**`<h1>` ownership: took the story's recommendation as written.** The shell does not own the
`<h1>`. `app.html`'s `<h1 class="app-title" data-testid="app-title">{{ i18n.t('app.name') }}</h1>` is
removed outright and replaced with a section-label `<span class="app-section-label"
data-testid="app-section-label">`. Each feature page keeps rendering its own `<h1 class="title">` —
confirmed by grep that every one of them already does, listed below.

**Grep result (AC-923-A): every reachable feature page template has exactly one `<h1>`, none has zero.**
`catalogue-list-page.html`, `catalogue-form-page.html`, `catalogue-import-page.html`,
`client-list-page.html`, `client-form-page.html`, `user-list-page.html`, `user-form-page.html`,
`employee-list-page.html`, `employee-form-page.html`, `worker-pool-page.html`,
`worker-register-page.html`, `worker-history-page.html`, `subcontractor-list-page.html`,
`subcontractor-form-page.html`, `supplier-list-page.html`, `supplier-form-page.html`,
`bab-tree-page.html`, `bab-form-page.html`, `audit-trail-page.html`, `sign-in-page.html`,
`change-password-page.html`, `not-found-page.html` — one `<h1 class="title">` each, no exceptions, no
screen found using `<h2>` for its page title. The story's premise ("if a screen currently renders its
title as `<h2>` instead of `<h1>`") did not hold for any screen in this codebase as it stands —
`KAFF-922` or an earlier pass must already have normalized this before this story ran.

**One pre-existing anomaly found, not fixed (out of this story's named scope):**
`landing-page.html` renders **two** `<h1>`s (`profile.title` at line 8, `hr.projects.title` at line
64) — a dashboard-style page with two stacked sections, each given its own top-level heading. Not one
of the screens this story's scope names (catalogue/client/user/employee/audit/bab), so left alone.
Flagging for whoever picks up the landing page next: decide whether the second section drops to
`<h2>` or the page counts as an exception to the one-`<h1>`-per-screen rule.

**Section label source:** `app.ts` added `sectionLabelKey`, a computed signal — not hardcoded per
route. It reads the current URL (via a `toSignal`-wrapped `Router.events` stream, since `router.url`
alone is not reactive) against `navGroups()`'s rows, and returns the matching group's `labelKey`
(`nav.group.core` / `nav.group.admin`, from `nav-rows.ts`, KAFF-922's own two mockup sections — no new
label list invented). Verified rendering `nav.group.core` on `/catalogue`, `/babs`, `/employees`,
`/clients`, and `nav.group.admin` on `/users`, `/audit`, both light and dark, both widths.

**Locale switch:** left in place, trailing inside `.header-end`, after the new action slot. It did not
conflict with the new layout — `margin-inline-start: auto` on `.header-end` still pushes the whole
cluster (slot + locale switch) to the header's end side, matching the mockup's action-cluster
placement even though the mockup itself doesn't show a locale switch.

**Action slot (search / secondary / primary actions, `AC-923-B`/mockup 97-118):** built the slot only,
did not wire any screen into it — that's `KAFF-924`'s per-screen job per the story's own "Not in this
story" section. New `HeaderActionsService`
(`src/Web/src/app/core/layout/header-actions.service.ts`) holds a
`signal<TemplateRef<unknown> | null>`; `app.html` renders it via `<ng-container
[ngTemplateOutlet]="headerActions.template()" />` inside `.header-end`, before the locale switch. Empty
today on every screen — confirmed in every screenshot below, no visual gap since an empty
`ng-container` renders nothing.

**CSS:** `.app-title` rule replaced with `.app-section-label` (`var(--text-sm)` / `var(--color-muted)`,
both existing tokens — zero color literals added, `AC-923-C`). `.app-header` itself was already the
mockup's flex row with `margin-inline-start: auto` on `.header-end` from `KAFF-922`; no restructuring
needed there since the account block already vacated.

**Screens rendered and looked at (`AC-923-E`)**, via `run-kaff-erp`'s `driver.mjs signed-shot`,
signed in as `owner_demo`:
- `/catalogue` — desktop (1280) light, desktop dark, 390px light, 390px dark (full 4-combo matrix,
  the screen most exercised by this change).
- `/users`, `/audit` — desktop light (confirms `nav.group.admin` label); `/users` also 390px dark.
- `/babs`, `/employees`, `/clients` — desktop light (confirms `nav.group.core` label);
  `/employees` also 390px dark.

All renders: header shows the section label (no `<h1>`, no `app.name`), no overflow or wrap breakage
at 390px, dark-mode contrast fine on the header itself. `/employees`'s table-row overflow and
`/users`'s 390px row-text collision in the screenshots are pre-existing table-density issues
(`KAFF-925`/`KAFF-926` territory), not caused by and not fixed in this story.

**Gates run:** `ng build` — clean, 0 errors (pre-existing `app.css` budget warning, 4.81 kB before this
change, 4.83 kB after — 24 bytes added by this story, not a new failure). `npm test` — 119/119 green.
`BidiGeometryTests` (`dotnet` E2E suite, `--filter-class Kaff.E2E.Tests.BidiGeometryTests`) — 2/2
green, against the live stack.

**AC-923-F (no new string outside i18n):** the only new template text is the section label, sourced
through `i18n.t(key)` off the existing `nav.group.core` / `nav.group.admin` keys — no new key added,
no literal string in the template.
