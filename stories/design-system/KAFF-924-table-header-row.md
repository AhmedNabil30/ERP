# KAFF-924 · Table header row — the shared fix, not a per-screen patch

<!-- kaff id=KAFF-924 slice=design-system points=3 state=BUILT -->

**Slice:** Design system · **Epic:** Apple-grade restyle · **Points:** 3.
**Depends on:** `KAFF-901` (`kaff-table-row`, BUILT).
**Files:** `src/Web/src/app/shared/kaff-table-row/kaff-table-row.ts` + `.html` + `.css` (new sibling
component, e.g. `kaff-table-header`, lives in the same folder).
**Reference:** `.design/Main.dc.html` lines 137-150 (header row + group heading with margin pill).

## Story
As a user of any dense list, I want one column-header row above the data instead of every row
repeating its own column label, so the table reads like the design instead of a mobile-stacked
layout left on at desktop.

## Gap, as found
`kaff-table-row.css` has no breakpoint at all — the per-row `<span class="row-price-label">` (e.g.
`catalogue-list-page.html:75`, text "سعر التكلفة") always renders, at every width. This is the
mobile stacked-card pattern, switched on permanently. Group headings (e.g.
`catalogue-list-page.html:56`, `<h2 class="group-title">`) are a plain heading with no margin pill,
no item count in the mockup's shape.

Mockup (`Main.dc.html` 137-144): one grid row, same `grid-template-columns` as the data rows, muted
12px bold labels, numeric columns (`سعر التكلفة`, `سعر البيع`) `text-align: end`, above ALL the data
rows (not repeated per باب group — check the mockup again: the header row appears once, before the
first group heading, not once per group).

Mockup group heading (146-150, 188-192): باب name (15px, 600), then an **orange pill** (`هامش ١٥٪`,
`--color-brand`-family bg/fg per KAFF-900's tokens, not the literal hex in the mockup) showing the
باب's margin percentage, then muted item count (`٨٤ بندًا`).

## What to do
1. **New component `kaff-table-header`**: takes the same `columns` grid-template string
   `kaff-table-row` takes, plus an ordered list of column defs (`{ labelKey: string; align?: 'start'
   | 'end' }`). Renders one header row, numeric columns `text-align: end`. This is the single
   source — every list's header comes from here, not hand-copied markup per screen.
2. **`kaff-table-row` mobile behaviour**: move the per-row `<span class="row-price-label">` pattern
   out of caller templates and into `kaff-table-row` itself as a documented mechanism — the row
   component should accept per-cell label text (or the caller keeps supplying `.row-price-label`
   spans, your call on the exact API) that is `display: none` above the mobile breakpoint (check
   `apple-erp-design` §5 for the breakpoint value already in use elsewhere in the codebase — reuse
   it, don't invent a new one) and shown, stacked, below it. **Fix this once, in the shared
   component's CSS** — not per screen. This is the actual bug: it was never screen-specific.
3. **Group heading**: either extend `kaff-table-row`'s folder with a `kaff-group-heading` component,
   or a plain reusable template partial — baba name + `kaff-badge` (reuse KAFF-901's badge component,
   check if it supports the pill shape/orange tone already, add a tone if it doesn't) + muted count
   span. One component, every list's group heading calls it.
4. Do not touch any feature screen's template in this story — that's `KAFF-925`. This story only adds
   the shared pieces and proves them with the component's own test/story fixture (if the codebase has
   a component-level test file convention, follow it; if not, a minimal spec asserting header text +
   alignment + mobile label visibility toggle is enough — check `kaff-table-row`'s existing test file
   if any, follow its pattern).

## Acceptance criteria
**AC-924-A** `kaff-table-header` renders one header row, numeric columns `text-align: end`, reusing
the exact `columns` grid-template contract `kaff-table-row` already uses (same string, same grid, so
header and rows line up).
**AC-924-B** `kaff-table-row`'s per-row column labels are hidden above the mobile breakpoint and
shown, stacked, below it — verified by rendering both widths, not by reading the CSS.
**AC-924-C** Group heading component/partial renders name + pill + count in one call; pill uses a
`--color-brand`-family token, not a literal hex.
**AC-924-D** Zero colour literals in any new/changed file.
**AC-924-E** No `data-testid` regression — this story adds new shared components, it does not remove
any existing test id from `kaff-table-row` itself.
**AC-924-F** Screenshotted light+dark, desktop+390px, actually rendered — a bare fixture page or one
converted screen is enough to prove the component, full per-screen rollout is `KAFF-925`.
**AC-924-G** `ng build` clean, `npm test` green.

## Not in this story
Wiring catalogue/babs/employees/clients/users/audit onto these new components — that's `KAFF-925`,
which depends on this one being `BUILT` first.

## Questions for Karim
None.
