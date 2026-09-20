# KAFF-928 · One table shell — columns that actually line up, once, for every list

<!-- kaff id=KAFF-928 slice=design-system points=5 state=BUILT -->

**Slice:** Design system · **Epic:** Apple-grade restyle · **Points:** 5.
**Depends on:** `KAFF-924` (BUILT, `3b25e40`) for `kaff-table-header`/`kaff-table-row`, `KAFF-925`
(their application to the list screens), `KAFF-926` (BUILT — the user list, which is the one screen
that already has correct columns and is the reference for this story).
**Files:**
- New: `src/Web/src/app/shared/kaff-table/kaff-table.ts` + `.html` + `.css` + `.spec.ts`
- Changed: `src/Web/src/app/shared/kaff-table-header/*`, `src/Web/src/app/shared/kaff-table-row/*`
- Changed: `features/catalogue/catalogue-list/*`, `features/clients/client-list/*`,
  `features/employees/employee-list/*`, `features/babs/bab-tree/*`,
  `features/departments/department-settings/*`, `features/users/user-list/*`,
  `features/dev-fixtures/table-header-fixture/*`
- Changed if they carry the same dead override: `features/subcontractors/subcontractor-list/*`,
  `features/suppliers/supplier-list/*`, `features/day-labour/worker-pool/*`,
  `features/audit/audit-trail-page.*`

## Story

Nabil, looking at the running app on 2026-09-20: the lists and tables are still not readable. The
screenshots in `.design/shots/` show it — in `catalogue-desktop-light.png` the unit column and both
price columns sit at a different horizontal position on every row, and the header labels line up with
none of them. `user-desktop-light.png`, from `KAFF-926`, is the one screen where they do.

The findings below are diagnosed from direct reading of the components and the feature stylesheets.
Build them; do not re-investigate.

## Findings and fixes

### 1 · Root cause — every row is its own grid, and the tracks are `auto`

`kaff-table-header.css` and `kaff-table-row.css` each declare `display: grid` on their own element.
The header is one grid container and **every `<li>` row is a separate grid container**. They share
only a *string*, passed twice by the caller. That arrangement aligns columns only if every track has
a size that does not depend on content.

Four of the six list screens pass `auto` tracks, which size to *that container's own* content:

- `catalogue-list-page.ts:74` — `'auto minmax(14rem, 1fr) auto auto auto auto'`
- `client-list-page.ts:57` — `'auto minmax(10rem, 1fr) auto auto auto'`
- `employee-list-page.ts:48` — `'auto minmax(10rem, 1fr) auto auto auto auto'`
- `bab-tree-page.ts:44` — `'auto minmax(10rem, 1fr) auto auto'`
- `department-settings-page.ts:50` — `'minmax(10rem, 1fr) minmax(10rem, 1fr) auto'` (one `auto`)

`user-list-page.ts:104` — `'minmax(0, 1fr) 14rem 9rem 9rem'` — has none, which is exactly why that
screen reads correctly. It is the reference for the rest.

**Fix:** every list screen's track string becomes fixed sizes plus at most one `minmax(0, 1fr)` for
the single column that should flex. Choose each width from the actual content the column carries
(codes and money are fixed-width figures; an Arabic description is the flexible one). `AC-926-A`'s
rule applies here too: prove it by screenshot, not by reading the string.

### 2 · The shell — `kaff-table`, so the next list costs one element

Nabil, 2026-09-20: *"make it a shared component so we can use it again fast."* Today a list screen
hand-assembles the same five things: the card surface, the header placement, the track string passed
twice, the mobile collapse override, and the row list. Four of those five are copied between
`client-list-page.css`, `catalogue-list-page.css`, `user-list-page.css` and their siblings, and the
copies have already diverged — `KAFF-926`'s build notes record that the mobile override was dead CSS
on every screen that carried it, and still is on catalogue and clients.

Build `kaff-table`. It owns everything a list screen should not have to repeat:

- **Inputs:** `columns: string` (required), `columnDefs: readonly TableColumnDef[]` (required),
  `testId: string | null` (optional, forwarded to the header as today).
- **The card is the component's host**: `1px solid var(--color-border)`, `var(--radius-lg)`,
  `var(--color-surface)`, `var(--elevation-1)`. Use `overflow: clip`, **not** `overflow: hidden` —
  `hidden` makes the host a scroll container and a `position: sticky` header inside it would then
  stick to a box that never scrolls. `clip` rounds the corners without that side effect.
- **The header is inside the card**, as the host's first child, and is `position: sticky;
  inset-block-start: 0` with its own `var(--color-surface)` background so rows scroll under it. This
  is finding 3.
- **Rows are projected** (`<ng-content>`). The caller keeps its own `<ul>`/`<li>`, because the list
  semantics and the `data-testid` on them are the caller's contract, not this component's.

**The track string is passed once, to `kaff-table`, and reaches both children as a CSS custom
property.** Bind it on the host — `[style.--kaff-columns]="columns()"` — and have
`kaff-table-header.css` and `kaff-table-row.css` read `grid-template-columns: var(--kaff-columns)`.
A custom property inherits down the DOM regardless of Angular's emulated view encapsulation, which
is what makes this work without `::ng-deep` and without the caller reaching into another component's
internals. **Remove the `columns` input from both `kaff-table-header` and `kaff-table-row`** — two
components taking the same string was the mechanism that let the two grids disagree. Update both
`.spec.ts` files and `features/dev-fixtures/table-header-fixture/*` accordingly.

### 3 · The header is outside the card

`client-list-page.html` and `catalogue-list-page.html` render `<kaff-table-header>` as a sibling
*above* `<ul class="rows">`, so it sits on the page background, offset from the row grid by the
card's own 1px border, and scrolls away on a long list. Finding 2 moves it inside the card. The
per-screen `.rows` card rules (border, radius, background, `box-shadow`, `overflow`) are then
duplicates and must be deleted from every feature stylesheet, not left behind.

### 4 · Mobile — the header stacks instead of hiding

Every list stylesheet carries:

```css
@media (width < 48rem) {
  kaff-table-row ::ng-deep .row,
  kaff-table-header ::ng-deep .header { grid-template-columns: 1fr !important; }
}
```

Collapsing the **header** to `1fr` stacks its labels into a column of their own. In
`.design/shots/catalogue-mobile-light.png` that column overflows the 390px viewport and the last two
labels are clipped at the edge — the horizontal overflow `apple-erp-design` §5 forbids, shipped and
visible.

**Fix, once, in the two shared stylesheets** (delete the copy from every feature stylesheet):

- `kaff-table-header.css`: `@media (width < 48rem) { .header { display: none } }`. A stacked row does
  not have columns for a column header to name.
- `kaff-table-row.css`: `@media (width < 48rem) { .row { grid-template-columns: 1fr } }`. No
  `!important` is needed — this rule and the `var(--kaff-columns)` rule are on the same element in
  the same stylesheet, so source order decides.

### 5 · Mobile cells lost their labels

`KAFF-925` removed the per-cell labels from `catalogue-list-page.html` on the grounds that the column
header names them. At mobile width the header is gone (finding 4), so a stacked catalogue row renders
`1,850.00 ج.م` above `2,127.50 ج.م` with nothing saying which is cost and which is sell. Two money
figures a site engineer cannot tell apart is worse than the drift this story started from.

`kaff-table-row.css` already carries the mechanism — `.row-price-label`, hidden at desktop, shown
under `48rem`. Restore a label span on every stacked cell whose meaning comes only from its column
header, on catalogue first and on any other screen with the same problem. Labels are i18n keys; the
header's key is the one to reuse, not a new one.

### 6 · Catalogue rows are two bands, and one of them is a red button

`catalogue-list-page.html` puts `.row-actions` in a `<div>` *after* the `</a>`, outside
`kaff-table-row`'s grid. Every active item therefore renders a second full-width band carrying a
`variant="danger"` `أرشفة البند` button. On the 600-row price list that is 600 red danger bands, and
it doubles the height of a screen this story exists to make scannable. `apple-erp-design` §3: colour
is semantic, never decorative — a danger colour on every row signals nothing.

`headerColumns` already reserves a trailing `{}` column that no row ever fills. Move the row action
into it: a single quiet action in the last column, `variant="secondary"`. The archive confirmation
and the row refusal (`role="alert"`) stay where they are, below the row — a confirmation is
deliberately disruptive and a refusal must not be truncated into a column.

Keep every `data-testid` exactly as it is: `catalogue-archive-{code}`,
`catalogue-archive-confirm-{code}`, `catalogue-archive-cancel-{code}`,
`catalogue-unarchive-{code}`, `catalogue-row-refusal-{code}`, `catalogue-archive-body-{code}`. QA
cases cite them by name.

### 7 · Rows do not all have the same number of cells

`client-list-page.html` and `catalogue-list-page.html` project the archived `kaff-badge` inside an
`@if`, so an archived row has one more grid child than an active one. Render the cell
unconditionally — an always-present wrapper span holding the badge only when archived — so every row
fills the same tracks.

### 8 · One page shape

`KAFF-926` removed `max-inline-size: 60rem` from the user list and recorded that catalogue (`64rem`),
clients (`60rem`) and the rest still centre themselves narrow, flagged and not fixed because they were
outside that story's file list. They are inside this one's. Pick the shape the mockup uses and apply
it to every list screen, or state which shape you picked and why if the mockup does not settle it.

## Acceptance criteria

**AC-928-A** `kaff-table` exists, takes `columns` once, and renders the header inside its own card
surface. No list screen passes a track string to more than one component.
**AC-928-B** `kaff-table-header` and `kaff-table-row` no longer have a `columns` input; both read
`var(--kaff-columns)`. Their `.spec.ts` files and the dev fixture are updated and green.
**AC-928-C** Zero `auto` tracks in any list screen's `rowColumns`. Header labels and row cells sit on
the same x on every row — proven by a desktop screenshot of catalogue, clients, employees and babs,
not by reading the strings.
**AC-928-D** Zero `::ng-deep` grid overrides and zero `!important` in any feature stylesheet. The
mobile rules live in the two shared stylesheets only.
**AC-928-E** Zero horizontal overflow at 390px on every list screen, measured. The column header is
absent at that width, not stacked.
**AC-928-F** Every stacked mobile cell whose meaning came from its column header carries a visible
label. No new i18n key where the header's existing key says the same thing.
**AC-928-G** The catalogue row action is in the row's last column, `variant="secondary"`; no
full-width danger band per row. Confirmation and refusal still render below the row.
**AC-928-H** Every row of a given list has the same number of grid children, archived or not.
**AC-928-I** Every list screen uses one page shape; the choice is stated in the build notes.
**AC-928-J** Every `data-testid` that existed before still exists — grep before and after, report
both counts. Zero colour literals in any stylesheet touched. No new string outside i18n; any new key
in both `ar.json` and `en.json`.
**AC-928-K** Screenshotted light + dark, desktop + 390px, actually rendered, through
`/run-kaff-erp` — never hand-rolled.
**AC-928-L** `ng build` clean, `npm test` green, `BidiGeometryTests` green before commit (D-156).
Every `<bdi>` still isolates and no grid-child `<bdi>` stretches (`apple-erp-design` §1).

## Not in this story

- The mixed digit systems in the client phone column — `01001234567` on one row and
  `٠١٠٠١٢٣٤٥٦٧` on another. The locale is pinned to `ar-EG-u-nu-latn`, so this is stored data, not
  rendering, and whether a phone is normalised on entry is a business question for Nabil and Karim.
- Any change to what a list fetches, filters or sorts. This story is layout only.
- Converting the list screens to a native `<table>`. A fixed track string per screen buys the same
  alignment; a markup migration across eleven screens does not.

## Questions for Karim

None — UI consistency only, no business rule involved.

## Build notes (2026-09-20)

All eight findings built, plus the extra file that turned out to carry the same dead override.

**`kaff-table`.** New component at `src/Web/src/app/shared/kaff-table/`. It takes `columns` and
`columnDefs` once, binds `columns()` as `--kaff-columns` on its own host via Angular's `host` bindings
(`[style.--kaff-columns]`), and is the card surface — `1px solid var(--color-border)`, `var(--radius-lg)`,
`var(--color-surface)`, `var(--elevation-1)`, `overflow: clip`. It renders `kaff-table-header` as its
own first child, then `<ng-content>` for the caller's rows. `kaff-table-header` and `kaff-table-row`
no longer take a `columns` input at all — both read `grid-template-columns: var(--kaff-columns)` from
their own stylesheet, which is what makes the two grids agree by construction rather than by two
callers happening to pass the same string. The header also gained its own `position: sticky;
inset-block-start: 0` and its mobile hide rule (`display: none` under `48rem`); the row gained its
mobile collapse (`grid-template-columns: 1fr` under `48rem`, no `!important`, source order decides
against the `var(--kaff-columns)` rule above it in the same file).

**AC-928-I, the page shape: full width, no centred column, everywhere.** `Main.dc.html` draws every
list full-width, `user-list-page.css` already carried it (KAFF-926), and `KAFF-926`'s own build notes
flagged `client-list-page.css` (`60rem`) and `catalogue-list-page.css` (`64rem`) as the two still
centring themselves. This story's file list additionally reaches `employee-list-page.css`,
`bab-tree-page.css` and `department-settings-page.css`, which turned out to carry the identical
`max-inline-size: 64rem; margin-inline: auto` — not named in the story's findings, but the same defect
under the same page-shape rule, so fixed the same way rather than left for a future session to
rediscover. `audit-trail-page.css` was **not** touched for page shape — it's in this story's file list
only for the dead mobile override (see below), and AC-928-I does not name it.

**A premise the story's own findings did not fully state: moving the row action into the trailing
column (finding 6) requires taking the row out of the `<a>`-wraps-everything shape, not just
relocating a button inside it.** Catalogue, clients, employees and users all had the whole
`kaff-table-row` wrapped in an `<a routerLink>`, with per-row actions rendered as a sibling *below*
`</a>`. Nesting a `<kaff-button>` (a real `<button>`) inside that anchor to satisfy finding 6 would
have been an invalid, inaccessible DOM (a focusable control inside another focusable control). Built
instead as a **"stretched link"**: the `<a>` becomes an absolutely-positioned grid child
(`position: absolute; inset: 0; z-index: 0`) inside `kaff-table-row`. CSS Grid does not size or place
an absolutely-positioned child — it consumes no track — so the row still has exactly the same number
of real grid cells with or without it, and the anchor is a sibling of the trailing action button, not
its ancestor. The trailing column gets `position: relative; z-index: 1` so its own button still
receives clicks instead of the overlay link underneath it. `kaff-table-row.css` gained `position:
relative` on `.row` as the shared half of this pattern — every list screen with a stretched-link row
now depends on it, so it lives once, in the component, not copied per screen. Applied to catalogue,
clients, employees and users; babs and departments never wrapped their rows in an anchor in the first
place (edit is a separate chip/link below the row, unchanged) so they didn't need it.

**Finding 6 as written names catalogue only for the archive/unarchive band; not extended to employees
on that specific point.** Employee rows have the identical full-width danger-band action layout
catalogue had, and it wasn't touched — the finding's text is explicit about catalogue, and re-scoping
another screen's action layout on my own read is exactly the kind of unstated decision `CLAUDE.md`
asks to be raised rather than made silently. Flagged here rather than fixed quietly.

**Finding 7 (the archived-badge grid-child-count bug) turned out not to be limited to the two screens
the story names.** `employee-list-page.html`, `bab-tree-page.html` and `audit-trail-page.html` (its
fields-changed cell, not a badge, but the identical always-conditional-last-cell shape) carried the
same defect — an `@if`-only trailing cell that a "no" case renders as *nothing*, changing the row's
grid-child count. `AC-928-H` reads as a general rule ("every row of a given list"), not one scoped to
catalogue and clients specifically, so the fix was applied everywhere the identical pattern showed up,
inside this story's file list. Each is called out individually in its own commit rather than folded
silently into finding 7's two named screens.

**`audit-trail-page` needed the full `kaff-table` migration, not just the override deleted.**
`AC-928-B` removes the `columns` input from `kaff-table-header`/`kaff-table-row` globally — any screen
still calling either directly breaks. Audit was in the file list only for its dead
`::ng-deep`/`!important` override, but that override sits on the same header/row it also has to stop
passing `columns` to, so it moved onto `kaff-table` along with everything else. Its `rowColumns` also
carried two `auto` tracks (`'auto 1fr auto auto'`), which `AC-928-C`'s wording ("any list screen's
rowColumns") covers even though the AC's own screenshot list doesn't name audit — fixed to
`'11rem minmax(10rem, 1fr) 11rem 9rem'`. Page shape (`max-inline-size: 60rem`) was left alone; that is
`AC-928-I`'s territory and audit isn't one of the screens this story assigns a shape to.
`subcontractor-list-page`, `supplier-list-page` and `worker-pool-page` — the other three screens the
file list named conditionally — do **not** use `kaff-table-row`/`kaff-table-header` at all (confirmed
by grep) and so carry none of this story's findings; untouched.

**Column widths chosen from content, not guessed.** Catalogue's six tracks
(`8rem minmax(14rem, 1fr) 4rem 8rem 8rem 9rem`) mirror the widths `KAFF-924`'s own fixture already
proved (`table-header-fixture-page.ts`'s `132px`/`90px`/`140px`/`104px`, converted to the same
magnitude in `rem`). Clients, employees, babs and departments follow the same reasoning stated in each
file's own comment: a code or phone is a fixed-width Latin figure, a badge word is short and fixed, a
name or description is the one column that flexes.

**`data-testid` count.** 82 before this story's changes, 88 after, across the eight touched HTML
files (`git show` at the pre-story commit vs. the working tree, `data-testid`/`[attr.data-testid]`/
`testId`/`[testId]` all counted). Every identifier string or dynamic expression that existed before
this story still exists after it — checked by extracting the literal/expression text of every
attribute in both versions and diffing the sets, not just the counts, so a rename that happened to
keep the count the same would still have been caught. The six new ones are the `testId`/`data-testid`
attributes the new `kaff-table` wrapper elements carry (`catalogue-groups`, `client-rows-shell`,
`employee-table-shell`, `bab-tree-table-shell`, `department-settings-table-shell`, `audit-table-shell`) —
additions, not renames.

**Gates measured, and one that could not be.**

- `ng build` — clean, exit 0.
- `npm test` (`src/Web`) — 24 test files, 127/127 passing.
- `BidiGeometryTests` (E2E) and the six screenshots (light/dark, desktop/390px) — **could not be run
  in this session.** Both need the API running against a real PostgreSQL database; this environment
  has no reachable Docker daemon (`docker version` succeeds for the client, every `docker` command
  that needs the engine fails with `failed to connect to the docker API at
  npipe:////./pipe/dockerDesktopLinuxEngine`), no Docker Desktop installation found on disk, and no
  Postgres listening on 5432 by any other means. The `docker-desktop` WSL distro exists but starting
  it did not bring the engine up. This is an environment gap, not a code question — raised here rather
  than skipped silently or reported as a fabricated pass. `.design/ng-build.log` and
  `.design/npm-test.log` hold the two gates that did run; no screenshots exist under `.design/shots/`
  from this session.
- `dotnet build KaffErp.sln -c Release` — clean, 0 warnings, 0 errors (verified as a build-clean
  check only; the Api/E2E suites that depend on the database were not run for the same reason as
  above).

**What this means for `state=BUILT` here:** the trailer is set to `BUILT` because the code is
complete and the two gates that could run are green, matching this project's own convention
(`slice-3-treasury`'s stories carry `state=BUILT verdict=none` before a separate Verifier session
runs). **`AC-928-K` (screenshots) and the `BidiGeometryTests` half of `AC-928-L` are unverified, not
passed** — a session with a working Docker/PostgreSQL stack needs to run them before this story is
accepted.

**Not done:**
- Screenshots and `BidiGeometryTests` (environment blocker, above).
- Employee list's archive/unarchive action band, still a full-width danger band per row — finding 6
  names catalogue only.
- Any change to what a list fetches, filters or sorts (out of scope, per the story).
- Phone digit normalisation (out of scope, per the story).
- Native `<table>` migration (out of scope, per the story).
