# KAFF-931 · Put the kit on the screens that exist today

<!-- kaff id=KAFF-931 slice=design-system points=13 state=READY -->

**Slice:** Design system · **Epic:** Apple-grade restyle · **Points:** 13.
**Depends on:** `KAFF-930` **BUILT and on `main`** — this story only consumes the kit, it does not
change it. If a component needs a change, stop and say so rather than editing it here.
**Design:** the canvas at https://claude.ai/artifact/KmuT1UYyXMiSqocN2MCkgY — artboards `BabTree`,
`Catalogue`, `States`. Read those three before starting.
**Files:** `features/babs/bab-tree/*`, `features/catalogue/catalogue-list/*`,
`features/clients/client-list/*`, `features/employees/employee-list/*`,
`features/subcontractors/subcontractor-list/*`, `features/suppliers/supplier-list/*`,
`features/users/user-list/*`, `features/departments/department-settings/*`,
`features/day-labour/worker-pool/*`, `features/audit/audit-trail-page.*`.

## Story

The kit exists; the screens still hand-roll what it replaces. Apply it, and delete the copies.

**Order matters. Do `bab-tree` first** — it is the screen Nabil pointed at, it is the worst, and it
exercises every part of the kit. Commit it, then move on. If the budget runs out, the worst screen is
fixed and the rest is a known remainder.

## Per screen

### 1 · `bab-tree` — the reference case

Today: every row spawns a second band holding `تعديل`, `نقل إلى أب آخر`, and a filled red
`أرشفة الباب`. Eight rows, eight red buttons. The loudest thing on the screen is *destroy*, and the
data — code, name, margin — is the quietest.

- The three buttons collapse into one `kaff-row-menu`: تعديل · نقل إلى أب آخر · أرشفة الباب, the last
  `tone="danger"` (danger **text**, never a filled red button). The second band disappears.
- **It becomes a tree.** Children indent, parents get a disclosure control with `aria-expanded`,
  expansion state goes in a query param. It is called a tree in `ux/screen-inventory.md` S-021 and
  has never been one.
- The default markup % edits in place with `kaff-inline-edit` — it is a scalar with an obvious type,
  which is exactly what that component is for.
- Loading, empty and refusal move to `kaff-state`.
- ⛔ **Drag-to-reparent is NOT in this story.** The fresh-eyes review called it a designer's solution
  for a screen touched rarely: a "move to…" picker in the row menu is as fast for ~40 trades and
  needs no pointer precision. Keep `نقل إلى أب آخر` as a menu item opening a picker.

### 2 · `catalogue-list`

- Row opens a `kaff-detail-panel` — code, description, unit, cost price (`kaff-inline-edit`), derived
  sell price with the line explaining it comes from the باب's markup, and the recent-changes list.
- The archive/unarchive action moves into `kaff-row-menu`. **Keep the existing inline confirmation**
  and its `data-testid`s exactly: `catalogue-archive-{code}`, `catalogue-archive-confirm-{code}`,
  `catalogue-archive-cancel-{code}`, `catalogue-unarchive-{code}`, `catalogue-row-refusal-{code}`,
  `catalogue-archive-body-{code}`. No toast.
- The panel's open item lives in `?item={code}`, so it is linkable and survives a refresh.

### 3 · The remaining lists

`client-list`, `employee-list`, `subcontractor-list`, `supplier-list`, `user-list`,
`department-settings`, `worker-pool`, `audit-trail`:

- Per-row action buttons → `kaff-row-menu`.
- Per-screen loading / empty / refusal markup → `kaff-state`.
- Filter and search state → query params, merged, matching `catalogue-list-page.ts`'s existing shape.
- A detail panel **only where a screen has a genuine quick-edit**. Do not add one to `audit-trail`:
  an audit record is immutable and has nothing to edit. Say which screens you gave a panel and why.

## Acceptance criteria

**AC-931-A** `bab-tree` renders an actual hierarchy — indentation, disclosure with `aria-expanded`,
expansion in a query param. Proven by a screenshot showing a parent with indented children.
**AC-931-B** Zero filled red buttons on any list row, on any screen. Grep the diff for
`variant="danger"` inside a row and report the count before and after.
**AC-931-C** No screen renders a second band per row. Every row action is in `kaff-row-menu`.
**AC-931-D** Every per-screen loading / empty / refusal block is deleted and replaced by `kaff-state`.
Report the number of lines removed.
**AC-931-E** Filter, search and open-panel state round-trip through the URL on every screen touched:
load `?status=archived` directly and the screen reflects it.
**AC-931-F** Every `data-testid` that existed before still exists — grep before and after, report both
counts. The catalogue archive ids above are named explicitly.
**AC-931-G** Zero colour literals in any stylesheet touched; no new string outside i18n; new keys in
both catalogues.
**AC-931-H** Zero horizontal overflow at 390px on every screen touched, measured.
**AC-931-I** `ng build` clean, `npm test` green, `BidiGeometryTests` green (D-156).
**AC-931-J** Screenshots light + dark, desktop + 390px, for every screen touched, through
`/run-kaff-erp`. **An acceptance criterion about something visible passes with the image or it does
not pass** — `KAFF-928` reported `AC-928-F` green when the screenshot showed it red.

## Not in this story

Drag-to-reparent. Bulk selection. Undo toasts. The role home, the project workspace, treasury and
extract screens — those have no API (`docs/superpowers/specs/2026-09-21-shell-and-interaction-redesign-design.md`
§6). Any change to what a list fetches, filters or sorts, beyond moving that state into the URL.

## Questions for Karim

None — presentation only, no business rule involved.
