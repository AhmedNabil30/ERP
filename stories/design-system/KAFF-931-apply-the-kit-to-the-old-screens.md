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

## Build notes (2026-09-24) — partial, bab-tree only

**State left at `READY`, not `BUILT`.** Only screen 1 of the story's list (`bab-tree`) is done;
`catalogue-list` and the seven remaining lists are untouched. Per the story's own instruction — "If
the budget runs out, the worst screen is fixed and the rest is a known remainder" — stopping here
after the mandatory-first, gated, committed screen rather than spreading a shrinking budget thin
across the rest. Commit: `d863a98`.

### bab-tree — done

- `src/Web/src/app/features/babs/bab-tree/bab-tree-page.ts` / `.html` / `.css` rebuilt on the kit.
- **Row menu**: every row's تعديل / نقل إلى أب آخر / أرشفة الباب collapsed into one `kaff-row-menu`
  per row (`bab-row-menu-{code}`), the archive item `tone="danger"` (text, not a filled button). The
  second band of per-row buttons is gone. `variant="danger"` inside the template: **2 before, 1
  after** — the one remaining is the existing inline archive-confirmation's own confirm button
  (`bab-archive-confirm-{code}`), the same shape `catalogue-list-page` already uses and the story
  explicitly keeps ("Keep the existing inline confirmation").
- **Real tree**: a parent with children gets a disclosure control (▾/▸) with `aria-expanded`;
  collapsed branches live in `?collapsed=<comma-separated باب ids>`, read once via
  `withComponentInputBinding`, written through the same `navigate()` `queryParamsHandling: 'merge'`
  helper `catalogue-list-page.ts` uses. Verified by direct URL load (`?collapsed=<CON's id>`):
  `CON-SUB` absent from the DOM, `aria-expanded="false"` on `CON`'s toggle, with no click involved.
  **Seed data has no بab with a child** — all 8 demo أبواب are root-level trades (CON, MAS, PLU, ELE,
  HVA, FIN, CAR, MET). A child (`CON-SUB`, code only, not a real trade) was created through the
  running API against the local dev database so AC-931-A's screenshot could show an actual parent/
  child pair; it is left in place as it is useful for the next screen's own testing and the API has
  no delete, only archive. Flagging this rather than silently deciding: **the bab-tree acceptance
  criterion could not be proven against the demo data as it stood.**
- **Inline edit**: default markup % edits in place via `kaff-inline-edit` (`type="decimal"`),
  committing through `BabsApi.edit` (which has no partial-update shape, so `nameAr`/`nameEn` travel
  unchanged). An unparseable percent or a server refusal rolls back and shows
  `bab.field.markup_format_invalid` or the server's own message, per the component's caller-driven
  rollback contract.
- **`kaff-state`** replaces the old bespoke loading/empty/refusal markup (`bab-tree-loading`,
  `bab-tree-empty`, `bab-tree-failure` test ids carried over onto `kaff-state`'s own `testId` input).
- **New i18n keys**: `a11y.expand_row`, `a11y.collapse_row` — both catalogues.
- **No detail panel on this screen.** The brief's own per-screen list for `bab-tree` names only
  row-menu, tree, inline-edit and `kaff-state` — no quick-edit exists here that a panel would add
  beyond what `kaff-inline-edit` already does in the row itself. Added and then removed once during
  the build; noting it so the next session does not treat its absence as an oversight.

### Test ids — before/after

Template call-sites carrying a test id (`data-testid=`, `[attr.data-testid]=`, `testId=`,
`[testId]=`): **10 before, 10 after.** Not a 1:1 id match, and that is a premise worth naming:

**A premise in the brief that does not fully hold.** AC-931-F says "every `data-testid` that existed
before still exists," but AC-931-C requires collapsing three per-row buttons into one `kaff-row-menu`
— and the kit's own contract (`KAFF-930`) names a menu item's test id as `{menuTestId}-{action}`, not
as a free-standing id a caller can choose. `bab-edit-{code}`, `bab-move-{code}` and
`bab-archive-{code}` — the three old trigger ids — could not both keep their exact old names **and**
live inside the row menu the same acceptance criterion demands; the two ACs pull in opposite
directions on those three ids specifically. Decided in favour of AC-931-C's structural requirement
(the row menu is what the story's Nabil-visible example is about) and renamed the three triggers to
`bab-row-menu-{code}-edit` / `-move` / `-archive`, keeping every other id — including the six-deep
confirmation/refusal chain (`bab-move-refusal-`, `bab-move-target-`, `bab-move-confirm-`,
`bab-move-cancel-`, `bab-archive-body-`, `bab-archive-confirm-`, `bab-archive-cancel-`,
`bab-archive-refusal-`) — byte-identical. Raising this rather than silently picking one AC over the
other.

### Not done

`catalogue-list`, `client-list`, `employee-list`, `subcontractor-list`, `supplier-list`,
`user-list`, `department-settings`, `worker-pool`, `audit-trail` — all nine remaining screens in the
brief are untouched, still on their pre-`KAFF-930` markup. Next session should read this story from
the top, not resume from a half-remembered state.
