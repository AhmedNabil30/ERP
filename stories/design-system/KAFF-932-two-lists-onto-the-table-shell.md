# KAFF-932 · Subcontractor and supplier lists onto the table shell

<!-- kaff id=KAFF-932 slice=design-system points=5 state=BUILT -->

**Slice:** Design system · **Epic:** Apple-grade restyle · **Points:** 5.
**Depends on:** `KAFF-928` (`kaff-table`) and `KAFF-930` (the interaction kit), both on `main`. This
story consumes both and changes neither. If a kit component needs a change, stop and report it
instead of editing it here.
**Design:** the canvas artboard `Main.dc.html` in `.design/` — the list direction: a thin divider per
row, no card per row, one quiet toolbar, figures in `bdi` with tabular numerals.
**Reference implementation already on `main`:** `features/employees/employee-list/*`. It is the same
shape (list + segmented filter + row menu + `kaff-state`) already converted. Read it first and follow
it rather than inventing a second dialect.
**Files:** `features/subcontractors/subcontractor-list/*`,
`features/suppliers/supplier-list/*`.

## Story

`KAFF-931` put the row menu and `kaff-state` on these two screens but deliberately left them on their
own hand-rolled row markup, because migrating to the `kaff-table` shell was out of that story's scope
(see its build notes, 2026-09-25). They are now the only two lists in the application whose columns
are not laid out by the shared shell, so their column edges do not line up with every other list and
their stylesheets still carry table geometry that `kaff-table` owns.

Migrate both onto `kaff-table` / `kaff-table-header` / `kaff-table-row`, and delete the geometry those
components replace.

## Acceptance criteria

**AC-932-A** Both screens render through `kaff-table`, `kaff-table-header` and `kaff-table-row`. No
`display: grid` or `grid-template-columns` declaring row geometry remains in either stylesheet.
Report the number of stylesheet lines removed per screen.
**AC-932-B** Column edges align with `employee-list` at 1440px, proven by a screenshot of each screen
at 1440px. The identity column leads with the name and demotes the code, matching `0a90ecb`.
**AC-932-C** Every `data-testid` that exists today still exists, including
`subcontractor-row-menu-{code}-edit` / `-archive` and `supplier-row-menu-{code}-edit` / `-archive`.
Grep before and after and report both counts. `subcontractor-list-page.spec.ts` and
`supplier-list-page.spec.ts` reference those ids — they must stay green without being weakened.
**AC-932-D** Filter and search state round-trips through the URL on both screens: loading
`?status=archived` directly is reflected in the rendered list.
**AC-932-E** Zero colour literals in either stylesheet. No user-facing string outside i18n; any new
key added to both catalogues.
**AC-932-F** Zero horizontal overflow at 390px on both screens, measured, not assumed.
**AC-932-G** `ng build --configuration production` exit 0 with no warning line, and `npm test` green.
Report the test file and test counts as measured.
**AC-932-H** Screenshots light and dark, desktop and 390px, for both screens, through
`/run-kaff-erp`. A visible criterion passes with the image or it does not pass.

## Not in this story

Any change to what either list fetches, filters or sorts, beyond moving that state into the URL. Bulk
selection. A detail panel — neither screen has a quick-edit that would justify one, and `KAFF-931`
already ruled on that. Any edit to `src/Api`, `src/Domain` or `src/Infrastructure`.

## Questions for Karim

None — presentation only, no business rule involved.

## Build notes (2026-09-28)

Both screens moved onto `kaff-table` / `kaff-table-header` / `kaff-table-row`, following
`employee-list-page.*` exactly — same stretched-link row pattern, same always-present trailing
wrapper (KAFF-928 finding 7), same identity-first cell shape.

**`subcontractor-list-page`** — columns `minmax(0, 1fr) 9rem 7rem 9rem 7rem`: name/code identity ·
phone · trade · retention % (`align: 'end'`) · trailing (row menu). Header labels reuse the existing
`subcontractor.field.name/phone/trade/retention_rate` keys — already in both catalogues, no new key
needed.

**`supplier-list-page`** — columns `minmax(0, 1fr) 9rem minmax(10rem, 16rem) 7rem`: name/code identity
· phone · address · trailing. `supplier.address` renders through an always-present `<span>` (`{{
supplier.address ?? '' }}`) rather than an `@if` — an absent address must still occupy its grid track,
or an address-less row and an addressed row would project a different number of grid children and the
columns to its right would drift. Header labels reuse `supplier.field.name/phone/address`.

**Column alignment, measured, not assumed (AC-932-B):** at 1440px, `subcontractor-list`'s computed
`grid-template-columns` is `590px 144px 112px 144px 112px` — identical, digit for digit, to
`employee-list`'s own `590px 144px 112px 144px 112px` (both full-width pages, same shell, same track
widths). `supplier-list` is a 4-column shape (`602px 144px 256px 112px`) but shares the same leading
identity edge and the same 112px trailing-column width. Verified with a one-off CDP probe script
(sign in, navigate, `getComputedStyle('.header').gridTemplateColumns`) rather than eyeballing a
screenshot — a first screenshot read looked like columns 2/3 were swapped between subcontractor and
employee; the DOM dump (`firstRowCells`) showed the code was correct all along (name, phone, trade,
retention, trailing — matching the TS) and the misreading was mine, not a bug. Screenshots:
`.design/shots/kaff-932/subcontractor-list-1440.png`, `supplier-list-1440.png`,
`employee-list-1440.png` (comparison reference).

**Page frame, KAFF-928 finding 8 / AC-928-I:** both stylesheets dropped `max-inline-size: 64rem;
margin-inline: auto` — the two screens were centred narrow columns before this story, which is why
their column edges could never have aligned with `employee-list`'s full-width frame regardless of the
row markup. Now full width, matching `employee-list-page.css`/`user-list-page.css`.

**Stylesheet diff (`git diff --numstat` against the pre-story commit):**
- `subcontractor-list-page.css`: 38 insertions, 41 deletions. Removed: `.row` (flex column), `.row +
  .row` (hairline border), `.row--archived`, `.row-link`, `.row-link > *` — the hand-rolled row
  geometry `kaff-table-row` now owns. Added: `.row-stretched-link`, `.row-trailing` (the trailing grid
  cell), matching `employee-list-page.css`.
- `supplier-list-page.css`: 39 insertions, 41 deletions, same shape.
- Zero `display: grid` / `grid-template-columns` declarations remain in either stylesheet — grepped,
  the only hits are two explanatory comments that say so.
- Zero colour literals in either file (grepped for hex/`rgb(`/`rgba(` — no matches; unchanged from
  before, both files were already token-only).

**`data-testid` counts, before → after** (`data-testid=` + `testId=` occurrences in each `.html`,
counted against the commit immediately before this story):
- `subcontractor-list-page.html`: 14 → 16. Every id that existed before still exists (grepped
  `subcontractor-row-menu-`, `-archive`, `-edit`, `-cancel`, `-confirm`, `-body`, `-refusal`, filter
  chips, create link — all present, unchanged strings). The two new ids are
  `subcontractor-table-header`/`subcontractor-table-shell` on the new shell wrapper, the same pair
  `employee-table-header`/`employee-table-shell` uses.
- `supplier-list-page.html`: 14 → 16, same two additions
  (`supplier-table-header`/`supplier-table-shell`).
- `subcontractor-row-menu-{code}-edit`/`-archive` and `supplier-row-menu-{code}-edit`/`-archive` are
  generated by `kaff-row-menu` itself from the `testId` prefix passed in — untouched, since the prefix
  string passed to each row's `kaff-row-menu` did not change.

**Spec files, kept green without weakening (AC-932-C):** both `*.spec.ts` files had one test
(`'puts the name before the code inside the identity cell'`) written against the old
`.row-link > .row-identity` wrapper, which no longer exists — the stretched-link pattern has no
wrapping `<a>`. Rewrote both to query `[data-testid="…-rows"] .row` (kaff-table-row's own rendered
`.row` div) and assert identity is the first grid child *after excluding* `.row-stretched-link`
(out-of-flow by `position: absolute`, same exclusion the CSS itself makes) — same assertion strength
(name-before-code, code in a `bdi`), adapted to the new DOM rather than deleted or loosened. Every
other test in both files needed no change. `npm test`: `Test Files 29 passed (29)` ·
`Tests 149 passed (149)`.

**AC-932-D, filter round-trips through the URL — measured:** loading `/subcontractors?status=archived`
and `/suppliers?status=archived` directly (fresh navigation, not a client-side filter click) rendered
the "archived" segment with `chip chip--on` and issued the `archived` list call — confirmed via a CDP
probe reading `document.querySelector('[data-testid*="filter-archived"]').className` after a direct
navigation. (Neither seeded database has an archived record, so the row count is correctly 0 — the
filtered-empty state, not a bug.) This mechanism was already in place before this story (via
`withComponentInputBinding`) and is unchanged by the table-shell migration; verified rather than
assumed since the migration touches the same template.

**AC-932-F, zero overflow at 390px — measured:** `document.documentElement.scrollWidth ===
document.documentElement.clientWidth === 390` on both screens (CDP probe, not eyeballed).
`kaff-table-row.css`'s own `@media (width < 48rem) { .row { grid-template-columns: 1fr; } }` handles
the collapse; this story added no new mobile rule.

**Gates measured:**
- `ng build --configuration production`: exit 0, no `warning` line.
- `npm test`: `Test Files 29 passed (29)` · `Tests 149 passed (149)`.

**Screenshots** (`.design/shots/kaff-932/`), via `run-kaff-erp`'s `signed-shot` as `owner_demo`:
`subcontractor-list-{light,dark}-{desktop,390}.png`, `supplier-list-{light,dark}-{desktop,390}.png`,
plus the three 1440px alignment shots named above.

**Not done / out of scope, as instructed:** no change to what either list fetches, filters or sorts
beyond the URL round-trip already in place; no bulk selection; no detail panel; no `src/Api`,
`src/Domain` or `src/Infrastructure` edit. The filter control itself stayed the hand-rolled `.chip`
markup rather than switching to `kaff-segmented-filter` — not required by any AC here, and swapping it
was extra surface area this story's scope does not ask for.
