# KAFF-923 · Top bar rebuild, and where the page `<h1>` lives

<!-- kaff id=KAFF-923 slice=design-system points=2 state=READY -->

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
