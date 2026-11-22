# Verification — design-system slice, 2026-11-22

**Verifier:** separate session, did not write the code. **Model:** claude-opus-5-5 (strongest tier).
**Scope:** the 28 stories with `slice=design-system` and `state=BUILT` on line 3 (KAFF-900, 901,
910–935). KAFF-936 is already `VERIFIED` and is out of scope.
**Tree:** `main` at `2eb5195`, working tree clean except the untracked `qa/shots-hold-release/`.
**Method:** static checks on the current tree (greps over `src/Web/src`), the ng specs as files
(not re-run: the caller reports `ng test` 603 green), and the running app (API 5080, web 4200)
measured with `.claude/skills/run-kaff-erp/driver.mjs layout` / `signed-text` / `signed-shot`.
No dotnet suite was run. Nothing was fixed, committed, or moved.

**Important context.** Several early conversion stories (910–921) name files that no longer exist:
`catalogue-form-page`, `employee-form-page` and `bab-form-page` were deleted later (`cf280a4`,
`9e943a4`, `f9be842`) and replaced by side panels. Those criteria are judged against the screen
that now serves the same route, and that substitution is stated per row. "Before/after" testid and
literal counts are historical claims; what can be checked today is the present state.

Findings are written below as they are reached.

## Findings log

### Static checks on the current tree (src/Web/src)

- **Colour literals.** Outside `styles.css`, the only hex/`rgb()` literals under `src/app` are four in
  `features/boqs/boq-page.css` lines 369–413, inside its print rules. `boq-page` is not a
  design-system story screen. Every story screen in this slice: **0 literals**. No inline colour in
  any `.html`/`.ts`.
- **Token set changed under D-175 (2026-10-06).** The client rejected the previous UI and a new
  language (`.design/v2/team-a/README.md`) replaced it. D-175 deletes `--color-brand` and the five
  `--sidebar-*` tokens outright and keeps `--color-interactive`/`--color-interactive-hover` only as
  legacy aliases onto `--btn`/`--btn-h`. The shell no longer has a top bar; the wordmark is an image
  `<img class="wordmark" src="/brand/kaaf-wordmark.png" alt="KAAF">`; the locale control is one
  button naming the other language. Every criterion that names those removed artefacts is therefore
  not met *as written*, by a later client-approved decision rather than by a defect. These are
  marked `NOT MET (superseded, D-175)` below, distinct from a real `NOT MET`.
- **`.danger-button`:** 0 occurrences in the application.
- **`!important` in feature stylesheets:** one live declaration,
  `features/catalogue/catalogue-list/catalogue-list-page.css:103` (`display: none !important`).
  The `user-list-page.css:67` hit is a comment.
- **`::ng-deep` in feature stylesheets:** `bab-tree-page.css` 82–83 (hides `.cell-en` below 1060px),
  257–258 (new-row tint), 477, 498 (button sizing); `bab-panel.css` 112, 119; `bab-move-dialog.css`
  102. None declares `grid-template-columns`; the 82–83 rule hides a grid child by `display: none`.
- **`<h1>`:** every feature `*-page.html` has 0 or 1. `landing-page.html` has two, but in mutually
  exclusive `@case` branches, so one renders. Pages with 0 render their title through
  `kaff-page-head` / `kaff-project-frame`.
- **Shell signals.** `app.ts` no longer has `navRows` or `toggleNav` (now `navGroups`,
  `openNav`/`closeNav`, `toggleNavCollapsed`); `showStaffNav`, `isNavOpen`, `signOut` remain.
  `data-testid`s `side-nav`, `account-menu`, `account-name`, `sign-out`, `locale-switch`,
  `nav-toggle`, `nav-backdrop`, `app-title` are present.
- **AC-125-D account block during forced password change.** The account block renders under
  `@if (session())`, independent of `showStaffNav()`, so the behaviour is present by reading, but no
  spec or E2E test asserts it; `app.spec.ts` has never contained a `sign-out` test (`git log -S`).

- **`variant="danger"` (filled) in rows.** Only inside an inline archive confirmation
  (`subcontractor-list-page.html:100`, `supplier-list-page.html:99`). D-174 keeps the destructive
  confirm filled; no filled red *trigger* sits on any row.
- **`danger-outline`** exists on `kaff-button` (`kaff-button.ts:17`, `kaff-button.css:94–102`, tokens
  `--err-ink`/`--err-bg`). Used by every destructive trigger on client-form, user-form,
  subcontractor-form, supplier-form. `kaff-button` has **no spec file**.
- **Observation (not an AC):** `user-form-page.html:443` styles *reactivate* as `danger-outline` and
  its confirm (`:430`) as filled `danger`. Reactivating a user is not destructive.
- **kaff-table shell.** `kaff-table-header` and `kaff-table-row` have no `columns` input; both read
  `--kaff-columns`, bound once on `kaff-table`. 17 feature templates render through `kaff-table`.
  No list screen's `rowColumns`/`--kaff-columns` contains an `auto` track. Subcontractor/supplier list
  stylesheets contain `grid-template-columns` only in a comment.
- **Contrast, computed from `styles.css` values (WCAG relative luminance):** `--err-ink` on `--bg`
  9.21:1 light, 13.13:1 dark; `--btn` (= `--color-interactive`) on `--bg` 14.50:1 light, 13.24:1 dark;
  `--danger-ink` on `--danger` 7.19:1 light, 9.01:1 dark.
- **Font, live (sign-in, unsigned).** `document.fonts.check('16px "IBM Plex Sans Arabic"')` true;
  loaded faces Plex 400/600 and Noto Kufi 500–700; zero resource entries to `fonts.googleapis.com` /
  `fonts.gstatic.com`.
- **Audit trail.** `GET /api/audit` has no paging (`core/audit/audit.api.ts:89`); `load_more` never
  existed in this screen's history (`git log -S load_more` empty). As owner on today's data the page
  renders 289,490 characters and 4,031 controls in one pass. Out of this slice's scope; recorded.

### Live layout, owner_demo, 390 and 1440 (driver `layout`)

PASS at both widths, `overflowPx` 0, `smallTargets` [] at 390, no raw keys, no ids:
`/employees`, `/workers`, `/clients`, `/clients/new`, `/users`, `/users/new`, `/audit`,
`/subcontractors`, `/subcontractors/new`, `/suppliers`, `/suppliers/new`, `/forbidden`,
`/nonexistent-x` (not-found), `/` (redirects owner to `/projects`), and for project فيلا 47
(`01a0de49-…`): `day-labour/pool`, `day-labour/new` (worker register), `day-labour/workers/{id}/history`.

Also PASS at both widths: `/babs` (a first run failed with a driver `innerText of null`; the re-run
passed), `/change-password`, `/catalogue/import`, `/catalogue`, `/catalogue?create=1`.

**URL round-trip, live (`signed-text`, 1440):** `?status=archived` changes the rendered count on
`/users` (10 to 2), `/subcontractors` (1), `/suppliers` (1), `/clients` (2), `/employees` (2).
Bab tree expansion lives in the `collapsed` query input (`bab-tree-page.ts:153`).

**Locale switch, live (`driver flow`):** `lang=ar dir=rtl` to `lang=en dir=ltr`, PASS.

**E2E test ids:** all 31 literal ids that `tests/E2E.Tests` references resolve in `src/Web/src/app`
(three are built dynamically: `client-filter-*` via `testIdPrefix`, `audit-change-*`; the fourth,
`status-guards`, is an API smoke id).

### Screenshots taken and looked at (scratchpad, not committed)

Employees 1440 light and dark (sidebar identical in both themes); bab tree 1440 light (parent with
chevron and an indented child `CON-SUB`); employees 390 dark; bab tree 390 light; users 390 dark;
clients 390 light; subcontractors 390 light; worker register 390 light; sign-in 390 dark, signed out;
catalogue 390 light and 1440 dark; client form 1440 light.

What the images showed that the probes did not:

1. **Users and subcontractors phone rows are the old stacked-cell collapse, not the README phone
   row.** Users at 390: name, then `المكتب الفني  المكتب الفني` (role and department, same words,
   no separator, no label), then the bare username, then the phone. Subcontractors at 390: a bare
   `5%` on its own line (the retention percentage, whose only label is the hidden column header) and
   the row menu `⋮` alone on a further line. Employees, babs and catalogue use the designed two-line
   phone row and read correctly.
2. **Catalogue desktop: the internal-cost tint is ragged.** `.cell-internal` is `justify-self: end`
   (from `.cell-end` in `kaff-table-row.css:48`), so the tinted band is the width of the figure, not
   of the 176px track; the `36,500.00` row's band is visibly wider than its neighbours. README
   "Kit additions" 3 describes a column tint.
3. **Catalogue desktop: cost and sell carry the same weight (`.money { font-weight: 500 }`).** README
   line 505 asks for "the sell price (plain)", so this matches the approved design; `AC-929-G`
   ("sell is the one 600 weight, cost muted") is superseded by it.
4. **Signed in and visiting `/sign-in`**, the screen renders two brand headers (the shell's phone
   header and the sign-in screen's own) and the menu bar. Signed out it is correct. Edge case;
   recorded.
5. **Data, not design:** one archived user's name and one Handover project's name render as
   `?. ???? ????` / `???? ????? r2`: seeded through a wrong encoding.


## Per-story verdicts

Legend: `MET` · `NOT MET` · `UNTESTED`. `NOT MET (superseded, D-175)` means the client-approved KAAF
language deliberately removed what the criterion names, and nobody updated the story text. It is not
a defect in the code. A story whose only gaps are superseded or untested is recommended `CONDITIONAL`.
A story with a real `NOT MET` is recommended `REJECTED`, with one stated exception (KAFF-934).

| Story | Pts | Recommended trailer | Failing / open ACs | Evidence |
|---|---|---|---|---|
| KAFF-900 tokens and shell | 5 | `VERIFIED verdict=CONDITIONAL` | A, C, D superseded. D-175 deleted `--color-brand` and `--sidebar-*`, the wordmark is now an `<img alt="KAAF">`, and the rebuilt shell renamed `navRows`/`toggleNav` | B MET: the sidebar is identical in the light and dark employees 1440 shots. E MET: 0px overflow at 390 on 23 routes. F MET: `--btn` on `--bg` measures 14.50:1 light and 13.24:1 dark |
| KAFF-901 shared components | 5 | `VERIFIED verdict=CONDITIONAL` | A: one of each exists, but `kaff-money-field` renders its own `<label>`, control and message. That is a second form-field implementation, and no decision records it | B MET: no `@Input`, `EventEmitter` or `*ngIf`; the 8 constructors only host effects. C MET: `kaff-field` uses `<label [for]>`, `ariaLabelKey` exists, `:focus-visible` is global, and no button or field sets `outline: none`. D, E MET |
| KAFF-910 catalogue list | 2 | `VERIFIED verdict=CONDITIONAL` | H superseded. J UNTESTED. The one live `!important` (`catalogue-list-page.css:103`) is in this file (see 928-D). The cost tint is ragged (finding 2) | A, C, D, E, I MET. B MET from the 390 light and 1440 dark shots |
| KAFF-911 catalogue form | 2 | `VERIFIED verdict=CONDITIONAL` | The named file was deleted in `cf280a4`, so this was judged on the create and item panels at `/catalogue?create=1`. I superseded: the README disables Save until something changes | A, C MET, layout PASS at both widths. Inputs go through `kaff-money-field`/`kaff-field` with `for` |
| KAFF-912 bab tree | 2 | `VERIFIED verdict=PASS` | none | A–D MET from the 1440 light and 390 light shots. I MET: the refusal is a `kaff-banner` with `tone="refusal"`, which renders `role="alert"` |
| KAFF-913 employee list | 2 | `VERIFIED verdict=PASS` | none | Shots at 1440 light and dark and 390 dark. Layout PASS. 0 colour literals |
| KAFF-914 employee form | 2 | `VERIFIED verdict=CONDITIONAL` | The named file was deleted in `9e943a4` and replaced by `employee-panel`, which this pass did not open. B, C, I UNTESTED on that successor | A MET: 0 colour literals |
| KAFF-915 worker registration | 2 | `VERIFIED verdict=PASS` | none | I MET: `smallTargets` [] at 390. B, C MET from the 390 light shot and layout |
| KAFF-916 client list | 2 | `VERIFIED verdict=PASS` | none | 390 light shot shows hairline rows, no card per row. `?status=archived` checked live |
| KAFF-917 client form | 2 | `VERIFIED verdict=PASS` | none | `/clients/new` layout PASS. Client form 1440 shot |
| KAFF-918 user list | 2 | `VERIFIED verdict=PASS` | none. The phone row's readability (finding 1) is raised under 928/929 | 390 dark shot. Layout PASS |
| KAFF-919 user form | 2 | `VERIFIED verdict=PASS` | none | `/users/new` layout PASS. `kaff-field` throughout |
| KAFF-920 audit trail | 2 | `VERIFIED verdict=PASS` | I is met only trivially: `load_more` never existed and the API has no paging. The page renders 289,490 chars at once; recorded | Layout PASS at both widths |
| KAFF-921 sign-in | 1 | `VERIFIED verdict=PASS` | none. Finding 4 is an edge case | Signed-out 390 dark shot. `sign-in` has no paste handler |
| KAFF-922 sidebar | 3 | `VERIFIED verdict=CONDITIONAL` | A: the tagline is superseded (D-175 removed `app.tagline`). C UNTESTED: no spec or E2E test proves the account block shows during a forced password change, and `app.spec.ts` never had one | B MET (`nav-rows.spec.ts:169`). E, F MET |
| KAFF-923 top bar | 2 | `VERIFIED verdict=CONDITIONAL` | B superseded: D-175 has no top bar | A MET: every page has 0 or 1 `<h1>`. Landing's two `<h1>`s sit in mutually exclusive `@case` branches. C MET |
| KAFF-924 table header | 3 | `VERIFIED verdict=CONDITIONAL` | A, B superseded by KAFF-928 (no `columns` input) and by the README phone row. Only the dev fixture uses the per-cell label mechanism | C, D, E MET |
| KAFF-925 header on six lists | 3 | `VERIFIED verdict=CONDITIONAL` | B superseded: 928-E says the header is absent at 390, not stacked | A MET: one header row on every list shot |
| KAFF-926 user list cleanup | 3 | `VERIFIED verdict=PASS` | none | E MET: `+ مستخدم جديد` is at the top on desktop and in the bar on a phone. F MET from the template. G MET live: the count goes from 10 to 2 |
| KAFF-927 locale segmented | 1 | `VERIFIED verdict=CONDITIONAL` | A, B superseded: D-175 makes it one button that names the other language | C MET (`driver flow` PASS). D MET (`locale-switch`, `locale-switch-mobile`) |
| KAFF-928 one table shell | 5 | **`BUILT verdict=REJECTED`** | **D NOT MET:** `catalogue-list-page.css:103` declares `display: none !important` (added in `0afbbed`, 2026-10-31). `bab-tree-page.css:82–83` also hides a grid child through `::ng-deep`. F: MET on catalogue; the users phone row has no labels (finding 1) | A, B, C, E, H MET. G superseded by 931 (row menu) |
| KAFF-929 page around the table | 8 | `VERIFIED verdict=CONDITIONAL` | G, H superseded: the README specifies a plain sell price and 48px rows | A MET: Plex resolves, 0 Google font requests, D-172 records it. B, C, F, I, J MET |
| KAFF-930 interaction kit | 8 | `VERIFIED verdict=PASS` | none. The `AC-930-B` spec runs under jsdom, which does not apply component CSS, so that spec cannot fail on `display: none`. The real proof is the CSS (`inline-flex`) | Specs for B–E exist and name their ACs. F MET: no toast, no snackbar |
| KAFF-931 kit on old screens | 13 | `VERIFIED verdict=PASS` | none | A MET: bab tree shot, `aria-expanded`, `collapsed` param. B MET: filled `danger` appears only on confirms. E MET live on 5 lists |
| KAFF-932 two lists on the shell | 5 | `VERIFIED verdict=PASS` | none. The subcontractor phone row is finding 1 | A MET: `grid-template-columns` appears only in comments. D, F MET live |
| KAFF-933 forms without the kit | 8 | `VERIFIED verdict=CONDITIONAL` | `bab-form` was deleted in `f9be842`, so this was judged on `bab-panel`. C UNTESTED: this pass did not exercise the validations | A MET: all five screens have 0 hand-rolled `class="label"` and 0 `<label>`. B MET. G MET from layout |
| KAFF-934 field screens and plain pages | 8 | `VERIFIED verdict=CONDITIONAL` (see note) | **A partly NOT MET:** the specialisation pill row was not built. It is held on `Q95`, which is still open | The rest of A is MET from the 390 shot: sticky header, back arrow, project name. B MET (`smallTargets` []). C MET: no row geometry in the pool or history CSS |
| KAFF-935 bordered destructive | 3 | `VERIFIED verdict=PASS` | none. `kaff-button` has no spec, so no unit test covers `danger-outline` | A MET (`kaff-button.css:94`). B MET: 0 `.danger-button`. C MET: confirms stay `danger`. D MET: 9.21:1 light, 13.13:1 dark |

**The KAFF-934 exception.** `AC-934-A` is not met, and by the letter of the rule that means
`REJECTED`. It is recommended `CONDITIONAL` instead: a pill row needs a trade list that `spec.md`
does not give, and CLAUDE.md says to stop and ask rather than invent one. The Scrum Master may apply
the strict rule.

## Totals

- **PASS:** 14 stories, 50 points (912, 913, 915, 916, 917, 918, 919, 920, 921, 926, 930, 931, 932, 935).
- **CONDITIONAL:** 13 stories, 52 points (900, 901, 910, 911, 914, 922, 923, 924, 925, 927, 929, 933, 934).
- **REJECTED:** 1 story, 5 points (928).

## Not reached

- This pass did not open or screenshot `employee-panel`, the screen that replaced KAFF-914's form.
- This pass did not exercise KAFF-933's validations live (`V-36-H` empty price, required pairs).
- This pass did not capture the open inline confirmation for KAFF-935-G. The confirm sits below the
  fold of the client form shot, so the trigger and confirm variants were judged from the templates.
- Only `owner_demo` was driven. Layouts specific to other roles are not verified here.
- No ng spec was mutation-tested, and no dotnet suite was run (per the brief).

## Money and permission rules (CLAUDE.md)

This slice is presentation only. No `.cs` file and no `core/**/*.api.ts` file in its scope was
examined as changed. This pass confirmed one thing only: no in-scope stylesheet or template hides a
permission-relevant field on the client (KAFF-913-I, 918-I). Nothing in this slice touches postings,
balances, the hold or approval.
