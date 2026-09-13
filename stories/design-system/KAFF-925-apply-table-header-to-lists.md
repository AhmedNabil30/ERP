# KAFF-925 · Apply the table header row to every converted list

<!-- kaff id=KAFF-925 slice=design-system points=3 state=READY -->

**Slice:** Design system · **Epic:** Apple-grade restyle · **Points:** 3 — six screens, mechanical
once `KAFF-924` exists.
**Depends on:** `KAFF-924` (`kaff-table-header` + fixed `kaff-table-row` mobile behaviour) — **not
pullable until `KAFF-924` is `BUILT`.**
**Files:** `catalogue-list-page.html`, `bab-tree` list markup (KAFF-912's file), `employee-list`
(KAFF-913's file), `client-list` (KAFF-916's file), `user-list` (KAFF-918's file), `audit-trail`
(KAFF-920's file) — locate each via its restyle story if the exact path isn't obvious.

## Story
As a user of any list screen, I want the same one-header-row, no-per-row-label table shape the
catalogue mockup shows, on every list, so the app is consistent instead of catalogue alone matching
the design.

## What to do
For each of the six screens:
1. Add `<kaff-table-header>` once, above the first group/list, with that screen's column defs.
2. Remove any per-row `<span class="row-*-label">` markup that duplicated the column header — the
   mobile-stacked version now comes free from `kaff-table-row`'s fixed CSS (`KAFF-924`), don't hand-roll
   it again per screen.
3. If the screen has group headings (catalogue's باب groups; check if bab-tree/employee-list/etc.
   have an equivalent — audit-trail and user-list may not), swap the plain `<h2>` for `KAFF-924`'s
   group-heading piece where a percentage/count actually exists to show; where there's no margin
   percentage (e.g. employee list has no "margin"), the pill is omitted — don't invent a number to
   fill it, name + count only.
4. Keep every `data-testid` — this is a per-screen version of `AC-910-E`, repeat it for each screen.

## Acceptance criteria — apply per screen, report per screen
**AC-925-A** Each of the six screens shows one header row, correct columns, `text-align: end` on
numeric columns.
**AC-925-B** Each screen's per-row labels are gone at desktop, present stacked at 390px.
**AC-925-C** Every `data-testid` that existed before still exists after — grep before/after per
screen, report both counts.
**AC-925-D** Zero colour literals introduced.
**AC-925-E** Each screen screenshotted light+dark, desktop+390px, actually rendered — six screens,
six sets of four screenshots minimum. **This is the exact thing the last wave skipped on 8 of 12
screens. Do not skip it here.**
**AC-925-F** `ng build` clean, `npm test`/`vitest` green after all six.
**AC-925-G** `BidiGeometryTests` green before commit (D-155 amendment: E2E joins the builder gates
while the Verifier is paused).

## Not in this story
Any screen not in the six named above. Any new column, any new data the API doesn't already return.

## Questions for Karim
None.
