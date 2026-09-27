# KAFF-932 · Subcontractor and supplier lists onto the table shell

<!-- kaff id=KAFF-932 slice=design-system points=5 state=READY -->

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
