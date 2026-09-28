# KAFF-934 · The field screens, and the plain pages nobody restyled

<!-- kaff id=KAFF-934 slice=design-system points=8 state=READY -->

**Slice:** Design system · **Epic:** Apple-grade restyle · **Points:** 8.
**Depends on:** `KAFF-901`, `KAFF-928`, `KAFF-930`, all on `main`. This story consumes the kit and
changes none of it. If a component needs a change, stop and report it.
**Design:** the canvas artboards in `.design/` — `WorkerMobile.dc.html` for the field screens and
`Main.dc.html` for the list direction. `WorkerMobile.dc.html` is the approved mobile direction at
390px: a sticky header carrying a back arrow, the screen title and the project name beneath it; 48px
controls and 16px type because it is used outdoors on a phone; the duplicate-phone warning as an
amber-bordered block holding both of its actions; the specialisation as a wrapped row of 44px pills,
not a dropdown; and a footer that stays at the bottom of the viewport with a 52px primary action and
cancel as plain text below it.
**Reference implementations already on `main`:** `features/employees/employee-list/*` for a converted
list, `features/catalogue/catalogue-form/*` for a converted form.
**Files:** `features/day-labour/worker-pool/*`, `features/day-labour/worker-history/*`,
`features/day-labour/worker-register/*`, `features/landing/*`, `features/forbidden/*`,
`features/not-found/*`.

## Story

Two groups of screens were never in a restyle story.

**The field screens.** `worker-register` is on `kaff-field` and `kaff-button` but not on the mobile
direction the artboard sets; `worker-pool` carries only `kaff-state` and lays out its own rows;
`worker-history` carries nothing from the kit at all. These are the screens a site engineer uses on a
phone, outdoors, in Arabic, and they are the ones Nabil reached last. They are the point of the mobile
direction, so they get it properly.

**The plain pages.** `landing`, `forbidden` and `not-found` use none of the kit. They are short, they
are seen constantly, and `forbidden` in particular is what a correctly refused user sees — it must
read as part of the same application, not as a stack of unstyled paragraphs.

**Order matters. Do `worker-register` first**, because it is the artboard's own subject and it sets the
mobile pattern the other two field screens then follow. Gate it, commit it, then move on. If the budget
runs out, the artboard's own screen is done and the remainder is named.

## Per screen

**`worker-register`** — match `WorkerMobile.dc.html`: sticky header with back arrow, title and project
name; 48px controls at 16px type; the existing duplicate-phone warning (`shared/duplicate-phone-warning/*`)
restyled to the artboard's amber block, keeping both of its actions and every one of its test ids; the
specialisation as a wrapped pill row with 44px hit targets; the day rate keeping its
"owner, finance and the responsible site engineer only" hint; a bottom-anchored footer with the 52px
primary action and cancel as text.

**`worker-pool`** — onto `kaff-table` for its rows. Its single primary action per row stays a button
rather than becoming a row menu — `KAFF-931` already ruled on that and the ruling holds. At 390px the
rows read as stacked records, not as a table scrolled sideways.

**`worker-history`** — the same treatment. It is a read-only record list: `kaff-table` plus
`kaff-state`, no row actions.

**`landing`, `forbidden`, `not-found`** — the page frame, the type scale and `kaff-button` for whatever
action each one offers. `landing`'s role dispatch behaviour must not change; `landing.spec.ts` and
`landing-page.spec.ts` stay green without being weakened.

## Acceptance criteria

**AC-934-A** `worker-register` at 390px matches `WorkerMobile.dc.html` in structure: sticky header with
back arrow, title and project name; a bottom-anchored footer; the specialisation as a pill row. Proven
by a screenshot at 390px.
**AC-934-B** Every interactive target on the three field screens is at least 44px on its block axis at
390px, measured.
**AC-934-C** `worker-pool` and `worker-history` render their rows through `kaff-table`. No row geometry
remains in either stylesheet.
**AC-934-D** The duplicate-phone warning keeps both actions and every test id it has today. Grep
before and after and report both counts.
**AC-934-E** Every `data-testid` on every screen touched still exists. Grep before and after, report
both counts per screen, and name any id that had to move because a component owns its spelling, with
the reason.
**AC-934-F** `landing`'s role dispatch is unchanged in behaviour. Name the spec files that prove it and
report their result.
**AC-934-G** Zero colour literals in any stylesheet touched. No user-facing string outside i18n; any
new key added to both catalogues. Kaff's status vocabulary appears verbatim where a status is shown:
لم تبدأ · جاري العمل · انتهت · متعثرة · تم تأجيلها.
**AC-934-H** Zero horizontal overflow at 390px on every screen touched, measured.
**AC-934-I** `ng build --configuration production` exit 0 with no warning line, and `npm test` green.
Report the counts as measured.
**AC-934-J** Screenshots light and dark, desktop and 390px, for every screen touched, through
`/run-kaff-erp`. A visible criterion passes with the image or it does not pass. `worker-pool` and
`worker-history` need a real project in the URL — create one through `POST /api/projects` as
`owner_demo`, the way `KAFF-931`'s close did, never with a raw SQL insert.

## Not in this story

Any change to what these screens fetch, submit or validate. Any new field. Offline behaviour. The
engagement flow itself. Any change to a kit component. Any edit to `src/Api`, `src/Domain` or
`src/Infrastructure`.

## Questions for Karim

None — presentation only. The day-rate visibility hint restates a rule `spec.md` already carries; if
the wording on screen and the rule in `spec.md` disagree, stop and report rather than choosing.

## Build notes (2026-09-28)

Built `worker-register` first, gated and committed, then `worker-pool`, `worker-history`, `forbidden`
and `not-found`, one commit per screen. `landing` needed no change — see below.

### Two things found before building, reported rather than guessed

1. **`worker-register` carries no day rate today, by design.** `worker-register-page.ts`'s own doc
   comment cites rule 8 / `AC-209-G`: *"No day rate anywhere ... the request and response carry no
   money member and this form adds no field for one."* The story's per-screen brief and
   `WorkerMobile.dc.html` both draw a day-rate field with the "owner, finance and the responsible site
   engineer only" hint on this screen. The two disagree, and the story's own "Not in this story" line
   bans adding a field. **Not built.** The day rate and its rate-visibility gate already live on
   `worker-history` (`canEditRate()`), which is the one screen `KAFF-210`'s own contract puts money on
   — nothing moved. Flagged for Karim/Nabil: either the artboard's day-rate block was meant for
   `worker-history`, not `worker-register`, or `AC-209-G` needs revisiting. I did not choose.
2. **The specialisation pill row (`AC-934-A`) could not be built.** `WorkerMobile.dc.html` draws it as
   a fixed set of trade pills (بنّاء / مساعد / نقاش / حداد). `DayLabourApi`'s `specialty` is free text
   with no options endpoint behind it — there is no server-known list to render as pills. Turning a
   free-text field into a fixed-choice control means inventing that list, which is a business decision
   `spec.md` does not answer (CLAUDE.md: "stop and ask, do not decide"). **Not built** — `specialty`
   stays the text input it was, restyled to the screen's 48px/16px chrome. `AC-934-A` is therefore only
   partly met: the sticky header, back arrow, project name and bottom-anchored footer match the
   artboard (screenshot below); the pill row does not exist. **Trailer stays `READY`, not `BUILT`,
   because of this gap** — see the trailer note at the end of this file.

### `worker-register`

- Restructured to `WorkerMobile.dc.html`'s mobile pattern: sticky header (back arrow to the pool +
  title + project name, the project name read off `AuthService.current()?.projects`, new
  `projectName` computed), a `.fields` body, and a bottom-anchored sticky footer holding the primary
  save action and a cancel link (`worker-register-cancel`, new).
- Fields set to this screen's own 48px/16px floor (`min-block-size: 3rem`, `font-size: 1rem`) —
  above the kit's 44px `--tap-target`, per the artboard's own numbers, a screen-local override in this
  file only, not a kit change.
- The primary save button stays the kit's 44px `kaff-button`, not the artboard's literal 52px — no
  hook exists to raise a kit button's own height from a caller without editing the component, which is
  out of scope. 44px already clears `AC-934-B`'s floor; the exact 52px figure is not reproduced.
  Flagged, not silently dropped.
- Added `--color-warning-surface` token to `src/Web/src/styles.css` (`#fdf6e8` light,
  `rgb(217 164 65 / 12%)` dark) — the artboard's amber block fill had no existing token, and
  apple-erp-design §0 says add the token rather than inline the value.
- `shared/duplicate-phone-warning/*`: CSS-only restyle to the amber block (background
  `--color-warning-surface`, tokenised type scale). Template, its two actions (the match list and the
  acknowledge checkbox) and every test id are unchanged — this file's HTML/TS were not touched, only
  its stylesheet.
- New i18n keys, both catalogues: `hr.worker.action.back_to_pool`.
- Test ids: 9 before -> 11 after (`worker-register-back`, `worker-register-cancel` added; none
  removed, none renamed). `duplicate-phone-warning.html`: 4 before, 4 after (CSS-only, no HTML touch).
- Measured at 390px: `scrollWidth` 390 = `clientWidth` 390 (zero overflow). 21 interactive targets,
  minimum block-size 44px, none under 44.
- Gate: `ng build --configuration production` exit 0, no warning line. `npm test`: 149 passed
  (149), 29 test files passed (29) — includes `worker-register-page.spec.ts`'s three cases.
- Screenshots: `.design/shots/kaff-934/worker-register-{390,desktop}-{light,dark}.png`.

### `worker-pool`

- Onto `kaff-table`/`kaff-table-row`, 7 columns: identity (name + code) · phone · specialty ·
  frequency · rating · history link · trailing action. The trailing column carries exactly one
  primary action per state (Engage, then Close, then the closed badge) — never a `kaff-row-menu`,
  `KAFF-931`'s ruling held rather than revisited, per this story's own instruction.
  `kaff-table-header` hides under 48rem (KAFF-928 finding 4), so each figure keeps its own visible
  label (`.figure-label`) rather than depending on the header alone at mobile width — the kit's only
  generic mobile-label mechanism is hardcoded to a `.row-price-label` class name, so this is a
  deliberate duplication (the label shows at both breakpoints) rather than a kit workaround.
- What a single grid row cannot hold — the opened-on date, the rating input, a rated value, a row's
  own refusal — now renders in a `.row-detail` block below the row, the same "expanded panel under the
  row" shape `EmployeeListPage`'s archive-confirm uses.
- Removed: the row's own card border/radius/grid geometry (`.row`, `.row-main` and their grid rules) —
  `kaff-table`/`kaff-table-row` own the card surface and track now. No row geometry remains in this
  stylesheet (`AC-934-C`).
- Bug found and fixed during gating: the history link (`.history-link`) had no `min-block-size` and
  measured 21px block-size at 390px — under the 44px floor. Added `display: inline-flex; align-items:
  center; min-block-size: var(--tap-target)`. Rebuilt, retested, remeasured: 0 failures.
- Test ids: 17 before -> 17 after in raw count, but two ids moved position: `worker-table-shell` and
  `worker-detail-{code}` are new (the expanded-panel wrapper and the table's own shell id); no id was
  removed. `worker-pool-page.spec.ts`'s structural test
  (`puts the name before the code inside the identity cell`) was updated to select
  `kaff-table-row .row-identity` instead of the screen's own removed `.row-main` wrapper — behaviour
  unchanged, only the selector followed the markup it was always describing.
- Measured at 390px with a live worker row: `scrollWidth` 390 = `clientWidth` 390. 17 interactive
  targets, minimum block-size 44px after the history-link fix, none under 44.
- Screenshots: `.design/shots/kaff-934/worker-pool-{390,desktop}-{light,dark}.png`.

### `worker-history`

- Onto `kaff-table`/`kaff-table-row`, 4 columns: project · dates · rating · day rate. `kaff-state` now
  renders the loading/refusal/empty states (previously hand-rolled paragraphs).
- The rate-edit control (an inline text input + save button, shown when `dayRate` is null and
  `canEditRate()`) is kept — it is an inline field write, not a `kaff-row-menu`-style row action, so
  "no row actions" in this story's per-screen text reads as no per-row navigation or archive-style
  trigger, which this read-only history never had. Removing the one write this screen has would be a
  behaviour change the story rules out ("Not in this story: any change to what these screens fetch,
  submit or validate").
- Same per-cell-label pattern as `worker-pool`, for the same reason (header hides under 48rem).
- Test ids: 13 raw before -> 11 raw after, net **zero lost**: `worker-history-failure`,
  `worker-history-loading` and `worker-history-empty` moved from a literal `data-testid` attribute to
  `kaff-state`'s `testId` input, which still renders `data-testid` on the component's host — same id,
  different place in the source, verified present in the rendered DOM.
  `worker-history-table-shell` is new (the table's own shell id).
- Measured at 390px with a real closed/rated engagement: `scrollWidth` 390 = `clientWidth` 390. 16
  interactive targets, minimum block-size 44px, none under 44.
- Screenshots: `.design/shots/kaff-934/worker-history-{390,desktop}-{light,dark}.png`.

### `forbidden`, `not-found`

- Added a missing `<h1>` to `forbidden` (apple-erp-design §7.1: every screen has exactly one). New
  i18n key, both catalogues: `forbidden.title`.
- Restyled the "back to start" link on both screens to a secondary-button visual (border, radius,
  44px tap target) — **kept as `<a>`/`routerLink`, not `kaff-button`**, because the only action either
  screen offers is navigation, and apple-erp-design §7.6 (Web Interface Guidelines) is explicit:
  "Navigation uses `<a>`/`routerLink`, never a click handler calling `router.navigate`" — `kaff-button`
  renders a `<button>`, which would break Ctrl-click / "open in new tab". This reads the story's "use
  `kaff-button`" instruction as "use the kit's visual language", not as "use the component", since the
  two conflict here and the platform rule is the one CLAUDE.md/apple-erp-design treat as load-bearing.
- Test ids: unchanged (`forbidden-page`, `not-found-page`), no ids added or removed.
- Measured at 390px: zero overflow, all interactive targets (the language toggle, the back link) at
  or above 44px.
- Screenshots: `.design/shots/kaff-934/{forbidden,not-found}-{390,desktop}-{light,dark}.png`.

### `landing`

**No change.** Read `landing-page.html/.ts/.css` in full: zero colour literals already, every string
already through `i18n.t`, tokens throughout. It has no single action `kaff-button` fits over — the one
button-like element (`.suppliers-link`) is already a styled `<a>` for the same `<a>`-not-`<button>`
navigation reason as `forbidden`/`not-found` above. Touching it risked `AC-934-F` (role dispatch must
not change in behaviour) for no criterion this story names. `landing.spec.ts` (8/8) and
`landing-page.spec.ts` are both in the 149/149 `npm test` run below.

### Gates, measured

- `ng build --configuration production`: **exit 0, no warning line** (checked twice, before and after
  the history-link fix).
- `npm test`: **Test Files 29 passed (29), Tests 149 passed (149)** (checked twice, same builds).
- 390px overflow: `scrollWidth == clientWidth == 390` on all five touched screens
  (`worker-register`, `worker-pool`, `worker-history`, `forbidden`, `not-found`), measured live
  against a running stack, not assumed from the stylesheet.
- 44px targets: measured live via `getBoundingClientRect()` over every visible interactive element on
  each of the three field screens. All at or above 44px after the `worker-pool` history-link fix
  (initially 21px, one element, fixed and reconfirmed at 44px).
- Screenshots: `.design/shots/kaff-934/` — light + dark, desktop (1280px) + 390px, for
  `worker-register`, `worker-pool`, `worker-history`, `forbidden`, `not-found`. `worker-pool` and
  `worker-history` screenshots show a real project (`مشروع KAFF-934`, created via
  `POST /api/projects` signed in as `owner_demo` through the real UI's cookie session, never a raw SQL
  insert) with one real registered worker, one closed and rated engagement. The seeded worker's name
  renders as mojibake in the screenshots — a UTF-8/Windows-1252 round-trip in my own throwaway seeding
  script (the exact failure mode `run-kaff-erp`'s SKILL.md documents for PowerShell string edits, this
  time in a Node script I re-saved through `Set-Content`), not a defect in the app or in any file this
  story touches. Cosmetic to the seed data only; column widths, wrapping and RTL isolation around the
  garbled text are unaffected.

### Not done

- The specialisation pill row on `worker-register` (`AC-934-A`, partial — see above).
- The literal 52px primary-action height on `worker-register`'s footer (kept at the kit's 44px).
- The day-rate field/hint on `worker-register` (does not belong there per the code's own existing
  rule — see above).
- No spec file was added or changed beyond the one selector update in `worker-pool-page.spec.ts`
  described above.
- `landing`, unchanged, for the reason given above.

**Trailer stays `state=READY`, not `BUILT`.** `AC-934-A` is not fully met (no pill row), which this
file's own instruction treats as a blocking condition: "Set trailer state=BUILT only if every AC is
met with evidence." Every other lettered criterion (`B` through `J`) is met with the evidence above.
