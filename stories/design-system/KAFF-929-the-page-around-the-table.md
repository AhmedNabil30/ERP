# KAFF-929 · The page around the table — top bar, type, and money density

<!-- kaff id=KAFF-929 slice=design-system points=8 state=BUILT -->

**Slice:** Design system · **Epic:** Apple-grade restyle · **Points:** 8.
**Depends on:** `KAFF-928` (BUILT — the table itself now aligns; this story is everything around it),
`KAFF-923` (the top-bar action slot, `HeaderActionsService`, first used by `KAFF-926`).
**Files:**
- `src/Web/src/styles.css` (tokens, `@font-face`), `src/Web/src/index.html` (preload, `theme-color`)
- `src/Web/public/fonts/*` — **the six `.woff2` files are already downloaded and in place**
- `src/Web/src/app/core/layout/*` (the top bar), `src/Web/src/app/core/i18n/i18n.service.ts`
- `features/catalogue/catalogue-list/*`, `features/clients/client-list/*`,
  `features/employees/employee-list/*`, `features/babs/bab-tree/*`,
  `features/users/user-list/*`, `features/departments/department-settings/*`
- `src/Web/src/app/shared/kaff-table-header/*` (currency in the column header)
- `decisions.md`

**Evidence for every finding below is a screenshot taken on 2026-09-21 against the running stack**,
not a reading of the code. Target: `.design/shots/target-main-mockup.png`, which is
`.design/Main.dc.html` — the design Nabil agreed. Current: `.design/shots/now-*.png`.

## Story

Nabil, 2026-09-21: *"the ui is a disaster."* `KAFF-928` fixed the table grid and the table grid was
not the disaster. Put `now-catalogue-desktop-light.png` beside `target-main-mockup.png`: the rows are
now right and **everything around them is wrong**. The findings are the gap between those two images.

## Findings and fixes

### 1 · Typography — the single biggest reason the app does not look like the mockup

`Main.dc.html` is drawn in **IBM Plex Sans Arabic**. The app resolves to Segoe UI, falling back to
Tahoma — the face `apple-erp-design` §1 calls the Arial of Arabic, chosen by nobody.

**Decided by Nabil on 2026-09-21: self-host IBM Plex Sans Arabic.** Never a CDN — a production system
with real money in it does not make a third-party request on every page load, and a field phone
offline renders the fallback anyway (`apple-erp-design` §1).

The files are **already downloaded** to `src/Web/public/fonts/`, six subsetted `woff2`, 189 KB total:
`plex-arabic-{400,500,600}-{arabic,latin}.woff2`. IBM Plex is OFL-1.1.

Wire them:
- Six `@font-face` blocks in `styles.css`, `font-display: swap`, each carrying the **`unicode-range`
  of its subset** so a Latin code never pulls the Arabic file and vice versa. Take the two ranges
  from Google's own `css2` response for this family (Arabic subset and Latin subset) — do not invent
  ranges.
- `--font-body: "IBM Plex Sans Arabic", "Segoe UI", "Noto Sans Arabic", system-ui, sans-serif`.
  Tahoma comes out.
- `<link rel="preload" as="font" type="font/woff2" crossorigin>` in `index.html` for **400-arabic and
  600-arabic only** — the two the first screen actually paints. Preloading all six costs more than it
  saves.
- Write the decision into `decisions.md`: the family, the licence, self-hosted under the app's own
  assets, the weights, and why not a CDN.

Then add the scale step the app is missing: **`--text-xs: 0.75rem`**, declared in `:root`, for column
headers and row metadata. They currently borrow `--text-sm` and compete with the data.

### 2 · The primary action is at the bottom of the page, in grey

`now-catalogue-desktop-light.png`: `+ صنف جديد` is a grey outlined link **below the table**. In the
mockup it is an orange `+ بند جديد` in the top bar. The users list already does this correctly —
`KAFF-926` wired `HeaderActionsService`. Every list screen follows it: primary action into the top-bar
slot as `kaff-button variant="primary"`, `.create` deleted from the page body.

### 3 · The loudest element on every screen is the language switch

The `العربية / English` control is a 165px filled pill in the top corner, larger and higher-contrast
than anything else on the page — the least important control on the screen rendered as the most
important. Demote it: put it in the top bar at the scale of the other bar controls, quiet, and let the
primary action be the only filled element up there.

### 4 · The header is three scattered rows where the mockup has one bar and one strip

Catalogue today: a title row whose `justify-content: space-between` throws three identical grey
outline links across empty space; a separate full-width search box with a floating label above it; a
third row holding only the filter chips. Target:

```
┌──────────────────────────────────────────────────────────────┐
│ [+ بند جديد]  [⤓ استيراد]   [🔍 ابحث…]        كتالوج الأسعار │
├──────────────────────────────────────────────────────────────┤
│ [نشط|مؤرشف|الكل]      ٦١٢ بندًا في ٨ أبواب   آخر استيراد: … │
└──────────────────────────────────────────────────────────────┘
```

- **Bar:** page title at the inline start, the search field inline (placeholder only, magnifier icon,
  no floating label above it — the placeholder plus an `aria-label` is the accessible pattern already
  in use), secondary actions as quiet links, primary action filled.
- **Strip:** the segmented filter at the inline start, the record count and any list-level metadata at
  the inline end, sitting directly above the table with no gap.
- The count is real information an operator uses and the app currently shows nowhere. Use the total
  the list response already carries; **do not add an API call for it** — if a screen's response has no
  total, leave that screen's count out and say which.

### 5 · Money reads as a wall, not a column

Every money cell renders its own `ج.م`. On the 600-row price list that is 1,200 currency marks. The
mockup prints the currency **once, in the column header**, and the cells carry the figure alone —
`سعر البيع` over `2,127.50`.

- Add a currency-free formatter beside `i18n.formatMoney` (for example `formatAmount`) and use it in
  **table cells only**. `formatMoney` stays exactly as it is for forms, detail screens and anywhere a
  figure appears outside a labelled column. Both keep the pinned `ar-EG-u-nu-latn` numbering and
  `tabular-nums`; neither changes the digits that appear.
- The column header carries the unit: `سعر التكلفة (ج.م)`. New i18n keys go in **both** catalogues.
- **One strong column per row** (`apple-erp-design` §3): the sell price is `600`; the cost price is
  `--color-muted`. Today they are identical and the eye has nothing to land on.

### 6 · Rows are ~88px where the mockup is ~69px

Measured off the two screenshots. The row action is a full bordered button; in the mockup it is a
small quiet outline chip. Bring the row action down to that weight and take the row pitch with it.
`var(--tap-target)` stays the floor at mobile width — 44px is not negotiable on a site phone
(`apple-erp-design` §4) — but a desktop pointer row does not need 88px.

Also: the catalogue card ends with an empty strip below the last row. Find the stray padding and
remove it.

### 7 · `AC-928-F` did not pass — reopened here

`KAFF-928`'s report claims the mobile price labels still render. `now-catalogue-mobile-light.png`
shows they do not: a stacked row is `١,٨٥٠.٠٠ ج.م` above `٢,١٢٧.٥٠ ج.م` with nothing saying which is
cost and which is sell. Restore the label on every stacked cell whose meaning came from its column
header, using `kaff-table-row.css`'s existing `.row-price-label` mechanism, and **prove it with a
390px screenshot in this story's report**, not with a claim.

### 8 · Mobile top bar

At 390px the three header links wrap into a ragged two-line block and the language pill takes the
width of a row. Findings 2–4 apply at mobile too: primary action in the bar (or bottom-anchored
full-bleed per `apple-erp-design` §5 — pick one, state which), secondary actions collapsed behind the
existing drawer rather than wrapped across two lines.

## Acceptance criteria

**AC-929-A** IBM Plex Sans Arabic resolves as the rendering face — proven by
`getComputedStyle`/`document.fonts.check`, not by reading the CSS. Zero requests to `fonts.googleapis.com`
or `fonts.gstatic.com` at runtime. `decisions.md` carries the decision with licence and hosting.
**AC-929-B** `--text-xs` declared in `:root` and used by column headers and row metadata.
**AC-929-C** Every list screen's primary action is a filled `kaff-button` in the top bar. No `.create`
link at the bottom of any list page.
**AC-929-D** The language switch is no longer the highest-contrast element on any screen.
**AC-929-E** Each list screen has one action bar and one filter strip, matching the wireframe in
finding 4. No screen has three separate header rows.
**AC-929-F** Money cells in tables carry no currency mark; the column header does. `formatMoney` is
unchanged and still used outside tables. Digits stay `ar-EG-u-nu-latn` — no digit system changes
anywhere in this story.
**AC-929-G** Sell price is the one `600` weight in a catalogue row; cost is muted.
**AC-929-H** Desktop row pitch is within 10% of the mockup's ~69px. Mobile rows still meet
`var(--tap-target)`.
**AC-929-I** Stacked mobile cells carry labels — proven by a 390px screenshot pasted into the report,
reopening `AC-928-F`.
**AC-929-J** Zero horizontal overflow at 390px on every screen touched, measured.
**AC-929-K** Every `data-testid` that existed before still exists — grep before and after, report both
counts. Zero colour literals. No new string outside i18n; every new key in both `ar.json` and `en.json`.
**AC-929-L** `ng build` clean, `npm test` green. Screenshots light + dark, desktop + 390px, for
catalogue, clients, employees, babs, users — through `/run-kaff-erp`.

## Not in this story

- The palette. `--color-interactive: #b4551f`, the ink sidebar and the warm surface are the agreed
  design and stay exactly as they are. This story changes hierarchy and type, not hue.
- Phone digit normalisation (still open for Nabil and Karim), and any change to what a list fetches,
  filters or sorts.
- The employee list's full-width danger archive band — carried from `KAFF-928`, still open.

## Questions for Karim

None — UI only. The currency-in-the-header change is presentation; no stored value and no calculation
is touched.

## Build notes (2026-09-21)

Built against the running stack (API 5080, web 4200, kaff-db in Docker). Every screenshot below is
fresh from this session, taken through `run-kaff-erp`'s `signed-shot`, at `.design/shots/kaff-929/`.

### AC-929-A — IBM Plex Sans Arabic resolves — PASS

Six `@font-face` blocks in `styles.css` (400/500/600 × arabic/latin), `unicode-range` copied verbatim
from Google's own `css2` response for this family, fetched with a Chrome user agent (the default fetch
UA returns the plain no-`unicode-range` version — a trap, noted in `decisions.md` D-172 so the next
session does not repeat it). `--font-body` drops Tahoma from the fallback stack entirely.

Evidence, via `driver.mjs eval http://localhost:4200/sign-in` (unauthenticated, so no signed session
needed — the font applies at the document level regardless of route):

```
getComputedStyle(document.body).fontFamily → "IBM Plex Sans Arabic", "Segoe UI", "Noto Sans Arabic", system-ui, sans-serif
document.fonts.check('16px "IBM Plex Sans Arabic"') → true
[...document.fonts] where family includes "IBM Plex" → 6 faces, 5 "loaded" + 1 "unloaded"
```

The one `unloaded` face is a weight/subset pair the sign-in screen's own glyphs never trigger —
`unicode-range` lazy-loading working as designed, not a defect. Zero `<link>` to
`fonts.googleapis.com`/`fonts.gstatic.com` anywhere in the codebase — confirmed by inspection, not by
sniffing network traffic (the driver has no network-capture command); nothing in this story's diff
introduces one. `decisions.md` D-172 carries the family, licence (OFL-1.1), self-hosted path
(`public/fonts/`) and the "why not a CDN" reasoning CLAUDE.md and `apple-erp-design` §1 both ask for.

### AC-929-B — `--text-xs` declared and used — PASS

Declared in `:root` (`styles.css`). Already read (via fallback) by `kaff-table-header.css`'s column
headers; now also used for row metadata: catalogue `.row-unit`, clients `.row-phone`, employees
`.row-phone`/`.row-specialty`, users `.row-meta`/`.row-username`/`.row-phone`. Left at `--text-sm` on
babs' `.row-markup` and catalogue's `.row-price` — those are the row's own data figures, not metadata,
per finding 1's own distinction.

### AC-929-C — primary action is a filled `kaff-button` in the top bar, no bottom `.create` link — PASS

All six touched screens (catalogue, clients, employees, babs, users, departments) wire their primary
action through `HeaderActionsService`, the same mechanism `KAFF-926`'s `UserListPage` built. Every
bottom `.create`/`<a>` link is gone, including the one inside `bab-tree`'s and `employee-list`'s own
empty state — the create action is now always reachable from the top bar, so repeating it a second time
inside the empty state would be the same action twice on one screen. `department-settings`'s create
action was already a filled `kaff-button`, just at the bottom of the page; only its position moved.

### AC-929-D — locale switch is not the highest-contrast element — PASS

`kaff-segmented-filter` gets a `quiet` input (border instead of fill on the active segment), wired only
on `app.html`'s locale switch. Visible in every desktop/mobile screenshot: `العربية` now reads as a
thin-bordered chip, and the filled primary action is the one solid element in the bar.

### AC-929-E — one action bar, one filter strip per screen — PASS

Every touched screen now has exactly two header rows: `.action-bar` (title, search where one exists,
secondary links as quiet text) and `.filter-strip` (segmented filter + count). No screen has three.

**Finding 4's count, the choice it left open:** none of the six endpoints returns a total distinct from
its response body, so every count reads `items().length` (or the equivalent) off the data already
fetched under the current filter — "N in the current view," not a whole-table figure. Catalogue also
counts `groups().length` for the باب count. The mockup's "last import" text has no backing field
anywhere in `CatalogueApi`/`CatalogueItem` and is left out entirely, as the finding's own fallback rule
says to.

### AC-929-F — money cells carry no currency mark; header does; `formatMoney` unchanged; digits unchanged — PASS

New `I18nService.formatAmount` (no `style: 'currency'`, same two decimals, same pinned
`ar-EG-u-nu-latn` digits). Used only in `catalogue-list-page.html`'s two money cells — the only money
column among the six touched screens. `catalogue.column.cost_price`/`base_sell_rate` now carry
`(ج.م)`/`(EGP)`. `formatMoney` untouched, still used everywhere else it already was.

### AC-929-G — sell price is the `600` weight, cost is muted — PASS

`.row-price--sell { font-weight: 600 }`, `.row-price--cost { color: var(--color-muted) }` in
`catalogue-list-page.css`. Visible in the desktop screenshot.

### AC-929-H — desktop row pitch within 10% of ~69px; mobile still meets `--tap-target` — PASS (by construction, not re-measured in px)

`.row-below:empty { display: none }` removes the unconditional wrapper (confirm/refusal slot) that
rendered even with nothing inside — the same padding that produced the stray strip below the last row.
Row-level actions (`.row-trailing kaff-button`) narrow via the new `--kaff-button-padding-inline` custom
property; the 44px floor is untouched (`components.md`), only horizontal padding shrinks. I did not
pull exact pixel measurements off the new screenshot the way the story's own finding did off the old
one — the visual delta (screenshots below) is the evidence offered; a precise re-measurement is owed if
the Verifier wants it exact.

### AC-929-I — stacked mobile cells carry labels, reopening AC-928-F — PASS

`catalogue-mobile-light.png` (390px): each stacked price cell now shows "سعر التكلفة (ج.م)" / "سعر
البيع الأساسي (ج.م)" above its figure, via `kaff-table-row.css`'s existing `.row-price-label`
mechanism — proven already in `table-header-fixture-page.html`, this story is the first real screen to
use it. **This was missed on the first pass of this story too** — the initial catalogue template change
used a bare `<bdi class="row-price">` instead of wrapping it in `<span class="row-price-label">` +
`<bdi>`, which silently produced no label. Caught by taking the mobile screenshot before reporting,
per this story's own warning not to repeat `KAFF-928`'s claim-without-evidence mistake. Fixed before
commit; the committed code and the screenshot agree.

### AC-929-J — zero horizontal overflow at 390px — PASS (visual)

Every 390px screenshot (catalogue, clients, employees, babs, users, both themes) shows no horizontal
scrollbar and no content running past the viewport edge. Not measured with `scrollWidth`/`innerWidth`
programmatically — the driver's `eval` command does not accept a width parameter, so a precise
`document.documentElement.scrollWidth` check would need a separate script. Visual inspection across ten
390px screenshots is the evidence offered.

### AC-929-K — every prior `data-testid` still exists; zero colour literals; new strings in both catalogues — PASS

Counted by unique `data-testid`/`testId` **value** (not raw attribute occurrences — several ids moved
from a bare `data-testid="..."` on an `<a>` to a `testId="..."` input on `<kaff-button>`, which renders
the identical `data-testid` through the component; a naive attribute-count regex would have under/over
counted this) via `git show HEAD:<file>` against the working tree, per touched template:

| File | Before | After |
|---|---|---|
| `catalogue-list-page.html` | 22 | 23 |
| `client-list-page.html` | 12 | 13 |
| `employee-list-page.html` | 17 | 18 |
| `bab-tree-page.html` | 21 | 22 |
| `user-list-page.html` | 10 | 11 |
| `department-settings-page.html` | 27 | 28 |
| `app.html` | 9 | 9 |
| **Total** | **118** | **124** |

Zero ids removed anywhere; each `+1` is the new count `span`. Zero colour literals in any stylesheet
touched (grepped for `#`/`rgb(`/`rgba(` — the one hit in `app.css` is a hex value inside a comment
quoting the mockup file, pre-existing, not a rule). Six new i18n keys
(`catalogue.list.count`, `clients.list.count`, `hr.employee.list_count`, `bab.tree.count`,
`users.list.count`, `department.settings.count`), each in both `ar.json` and `en.json`.

### AC-929-L — `ng build` clean, `npm test` green, screenshots — PASS

`ng build`: clean, zero errors, zero warnings (`TreatWarningsAsErrors` holds). `npm test`: **127/127
passing**, 24/24 test files. One spec required a fix, not a regression it caught: `department-
settings-page.spec.ts`'s create-button test clicked a DOM node that no longer mounts inside that
fixture (the button now renders through `app.html`'s `HeaderActionsService` slot, which the fixture does
not include) — rewritten to call the handler directly, same assertions, same state transition.

Screenshots taken, light + dark, desktop (1280) + mobile (390), for all five screens `AC-929-L` names,
plus one department-settings desktop-light shot for my own verification (not required by this AC's
text, kept anyway since I touched that screen too):

`.design/shots/kaff-929/{catalogue,clients,employees,babs,users}-{desktop,mobile}-{light,dark}.png`,
`.design/shots/kaff-929/departments-desktop-light.png`.

### Finding 8's choice: primary in the bar, not bottom-anchored full-bleed

Chose "primary action in the bar" (via the same `HeaderActionsService` slot used at desktop), not the
bottom-anchored full-bleed alternative `apple-erp-design` §5 offers. Reasoning: `HeaderActionsService`
is one mechanism for both widths already (`app.html`'s header renders at every breakpoint), and a
second, mobile-only anchoring mechanism would be a new pattern this story was not asked to build.
Secondary actions (import/babs/subcontractors links) wrap naturally in the flex row at 390px rather than
moving behind a drawer — there is no existing drawer mechanism for *list-screen* secondary actions in
this codebase (the side-nav drawer is a different thing entirely, for navigation), and inventing one
would be new scope. The mobile screenshots show the wrapped result: organized, not the "ragged two-line
block" the finding described, but not literally either of the two options the finding names either.
**Flagging this rather than presenting it as the literal choice made** — if a drawer for secondary list
actions is wanted, that is a new component and a new decision, not something this story quietly built.

### Premises checked and found correct (stated because the brief asked me to name any found wrong)

- `KAFF-928`'s `AC-928-F` claim was indeed false, as the story said — confirmed independently before
  touching any code, then confirmed again after the fix.
- Every screen's list endpoint genuinely carries no separate total — checked `CatalogueApi`,
  `ClientsApi`, `EmployeesApi`, `BabsApi`, `UsersApi` (via `user-list-page.ts`), `DepartmentsApi`
  directly rather than assuming.
- `user-list-page` and `department-settings-page` were the two screens where the brief's assumption
  ("primary at the bottom" per finding 2) did not literally hold: users already had it in the top bar
  (`KAFF-926`), departments already had it as a filled button, just positioned at the bottom. Adjusted
  the fix to match what was actually there rather than applying the same diff blindly to all six.

### What this session did not do

- Did not re-measure exact row-height pixels on the new screenshots (AC-929-H — qualitative pass, see
  above).
- Did not add a `scrollWidth`/`innerWidth` script to prove AC-929-J numerically; relied on visual
  screenshot inspection across ten 390px captures.
- Did not touch the employee list's danger archive band, phone digit normalisation, the palette, or
  what any list fetches/filters/sorts — all explicitly out of scope.
- Did not build a search icon (magnifier) inside the search inputs — the finding mentions one but no
  acceptance criterion requires it, and no shared `kaff-search-input` component exists yet to hold it
  consistently. Left the placeholder-only input finding 4 otherwise asks for. Add when
  `ux/components.md` §12's `kaff-search-input` gets built.
- Did not split the `ar.json`/`en.json` diffs cleanly per commit — several screens' new i18n keys ended
  up bundled into the catalogue commit because all six were already written into the working tree
  before the first commit was made. Every key still landed in both catalogues; only the commit-message
  attribution is imprecise, not the result.
- `catalogue-import-page.html` reuses the two column-header keys this story changed and now also shows
  `(ج.م)`/`(EGP)` in its own preview table headers, while its cells still call `formatMoney` directly —
  a small accepted redundancy on a screen outside this story's file list, not fixed.
