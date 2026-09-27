# KAFF-933 · The forms that never got the kit

<!-- kaff id=KAFF-933 slice=design-system points=8 state=READY -->

**Slice:** Design system · **Epic:** Apple-grade restyle · **Points:** 8.
**Depends on:** `KAFF-901` (`kaff-field`, `kaff-button`) and `KAFF-930`, both on `main`. This story
consumes them and changes neither. If a component needs a change, stop and report it.
**Design:** the canvas artboard `CatalogueForm.dc.html` in `.design/`. It is the approved form
direction: one centred column at most 880px wide, one white card with a 16px radius and a 1px border,
44px control height, label above control, hint below it, the error replacing the hint rather than
stacking, primary and cancel actions at the start of a footer row and the destructive action pushed to
the far end as bordered text, never a filled red button.
**Reference implementation already on `main`:** `features/catalogue/catalogue-form/*` — the converted
form. Read it before starting and follow its dialect rather than inventing a second one.
**Files:** `features/babs/bab-form/*`, `features/subcontractors/subcontractor-form/*`,
`features/suppliers/supplier-form/*`, `features/catalogue/catalogue-import/*`,
`features/auth/change-password/*`.

## Story

Five screens hand-roll their own labels, inputs, hints, errors and buttons. `kaff-field` owns the
label, the hint, the error and the wiring between them, and its own documentation says nothing else
may render a label. These five do. The result is five slightly different field heights, five error
treatments and a destructive action that looks different on each one.

Convert each screen to `kaff-field` + `kaff-button`, keep every existing validation behaviour exactly
as it is, and delete the per-screen CSS those components replace.

**Order matters. Do `bab-form` first** — it is the largest of the five and it exercises the whole set
(a locked generated code, a required text pair, a decimal percent and a destructive action). Gate it,
commit it, then move on. If the budget runs out, the biggest screen is done and the remainder is
named.

## Per screen

**`bab-form`** — the locked code line follows `CatalogueForm.dc.html`'s treatment: a disabled-looking
field with a lock glyph and the hint that says it is generated and cannot be changed, not a bare
paragraph. Keep `bab-form-no-source` and its back link.

**`subcontractor-form`** and **`supplier-form`** — the same conversion, nothing more. Both carry a
danger zone; it becomes bordered danger text in the footer row, matching the artboard.

**`catalogue-import`** — the file picker and the result report are not form fields and stay as they
are structurally. What converts is the surrounding page frame, the buttons, and the validation
message list, which must read as one card, not three stacked bands.

**`change-password`** — the smallest. It is also the first screen a user with a temporary password
sees, so its error treatment must match every other form exactly.

## Acceptance criteria

**AC-933-A** Every label, hint and error on all five screens renders through `kaff-field`. Grep each
template for a hand-rolled `<span class="label">` and report zero.
**AC-933-B** Every button renders through `kaff-button`. No filled red button remains on any of the
five screens; a destructive action is bordered danger text.
**AC-933-C** Every validation behaviour that exists today still holds, unchanged: the same field is
still required, the same message still appears, an empty price is still refused rather than stored as
zero (`V-36-H`). State per screen which validations you exercised and how.
**AC-933-D** Every `data-testid` that exists today still exists. Grep before and after, report both
counts per screen, and name any id that had to move because a component owns its spelling — with the
reason, the way `KAFF-931` did.
**AC-933-E** Per-screen stylesheet lines removed, reported per screen. A conversion that adds CSS has
not converted anything.
**AC-933-F** Zero colour literals in any stylesheet touched. No user-facing string outside i18n; any
new key added to both catalogues.
**AC-933-G** Zero horizontal overflow at 390px on all five screens, measured. Each form is a single
column at that width.
**AC-933-H** `ng build --configuration production` exit 0 with no warning line, and `npm test` green.
Report the counts as measured, not as expected.
**AC-933-I** Screenshots light and dark, desktop and 390px, for all five screens, through
`/run-kaff-erp`. A visible criterion passes with the image or it does not pass. Include one image of a
field in its error state.

## Not in this story

Any change to what a form submits, validates server-side, or fetches. Any new field. Any change to a
kit component. Signal forms migration where a screen is on typed reactive forms today. Any edit to
`src/Api`, `src/Domain` or `src/Infrastructure`.

## Questions for Karim

None — presentation only, no business rule involved.

## Build notes (2026-09-28)

Built in order against the running stack (API 5080, web 4200, `kaff-db` in Docker), one commit per
screen: `95f5c4c` (bab-form), `bcefc07` (subcontractor-form), `82e1adb` (supplier-form), `ad63325`
(catalogue-import), `4fc2e2a` (change-password).

**Kit gap found, not guessed around — this is why the trailer stays `READY`.** `kaff-button`
(`src/Web/src/app/shared/kaff-button/kaff-button.css`) has four variants: `primary` and `danger`
are both filled backgrounds, `secondary` is a neutral border, `ghost` is text-only in the accent
colour. **None of them is a bordered *danger*-coloured button.** `CatalogueForm.dc.html`'s footer
draws the destructive action as exactly that (`color`/`border-color: #a52121`, `background:
transparent`), and `AC-933-B` requires it and forbids a filled red button. `client-form-page.html`
and `user-form-page.html` (outside this story's scope) already use `kaff-button variant="danger"`
for their own destructive actions — filled — which is the pattern this story was explicitly told
not to reproduce. Per `CLAUDE.md`/this run's hard rules, a kit change is a stop-and-report, not an
edit: `subcontractor-form-page.html` and `supplier-form-page.html` keep their archive trigger and
its confirm button as a plain `.danger-button` (bordered, `color`/`border: var(--color-danger)`,
`background: transparent`) rather than a filled `kaff-button variant="danger"`. Every other button
on all five screens — save, cancel, the re-import actions — renders through `kaff-button`. Reported
here rather than decided silently; the fix is either a new `kaff-button` variant (e.g.
`danger-outline`) or a ruling that a plain bordered button is acceptable for this one case.

### bab-form (`features/babs/bab-form/*`)

- Every label/hint/error now renders through `kaff-field`; the save action through `kaff-button`.
- The locked, generated code (edit only) follows `CatalogueForm.dc.html`'s treatment: a
  disabled-looking field (`.input-locked`) with a lock glyph (inline SVG, `currentColor`) and the
  `bab.field.code_not_editable` hint — not a bare paragraph, which is what it was before.
- No danger zone exists on this screen (none did before); nothing was added.
- Did not add a cancel button next to save — the original screen had none, and the artboard's
  cancel action isn't validation-bearing scope; adding new navigation behaviour is out of this
  story's "no new field" spirit. Flagging this as a deliberate omission rather than a silent one.
- Stylesheet: 94 → 35 net lines added, 59 removed (`git diff --stat 95f5c4c^ 95f5c4c`).
- `data-testid` count: 13 before, 13 after, same names (`bab-form`, `bab-form-no-source`,
  `bab-form-back`, `bab-code`, `bab-field-code`, `bab-field-name-ar`, `bab-field-name-en`,
  `bab-field-parent`, `bab-field-sort-order`, `bab-field-default-markup`,
  `bab-field-default-markup-error`, `bab-form-refusal`, `bab-submit`). None moved.
- Validations exercised: the default-markup-percent `required` error, driven through the field by a
  real DOM blur (not asserted from code) — screenshot below. Create-mode required fields (code,
  name AR/EN) render with `kaff-field`'s `*` marker; a full create was exercised indirectly through
  the subcontractor/supplier screens' own create-and-land-on-edit flow (same `[formField]`
  machinery). No money field exists on this screen, so `V-36-H` does not apply here.
- Screenshots: `.design/shots/kaff-933-bab-form-desktop-{light,dark}.png`,
  `kaff-933-bab-form-390-{light,dark}.png`, `kaff-933-bab-form-error.png` (the required-markup
  error — this is the story's one field-in-error image), `kaff-933-bab-edit-{dark,390}.png` (the
  locked code field, light-desktop capture is `kaff-933-bab-edit.png`).

### subcontractor-form (`features/subcontractors/subcontractor-form/*`)

- Every label/hint/error now renders through `kaff-field`. The read-only tax registration number
  (edit-only) now renders through `kaff-field` too, in the same locked-field look as bab-form's
  code line (no lock glyph — the artboard's lock is specific to a generated code, not a
  Finance-owned read-only number).
- Danger zone moved into the footer row per the artboard: save at the inline-start, the archive
  trigger pushed to the inline-end (`margin-inline-start: auto` on `.footer-danger`) as bordered
  danger text. See the kit-gap note above for why it is not a `kaff-button`.
- Stylesheet: 98 → 43 net lines added, 55 removed (`git diff --stat bcefc07^ bcefc07`).
- `data-testid` count: 16 before, 16 after, same names. `subcontractor-submit` and
  `subcontractor-archive-cancel` moved from a literal `data-testid` on a native element to
  `kaff-button`'s `testId` input, which renders the identical attribute — same id, same DOM
  position, nothing for a test to notice.
- Validations exercised live, through the running app: created a real subcontractor
  (`01012345678` / `مقاول اختباري`) via the actual create form and landed on its own edit screen
  (code `SC-10001`, generated); opened the archive confirm dialog. Duplicate-phone warning and the
  retention-percent format error were not driven live this session (no second subcontractor was
  created to collide with); the component and its schema are unchanged from the pre-conversion
  version, so the check is structural (same `schema()`, same `pattern`), not re-exercised.
- Screenshots: `kaff-933-sub-form-desktop-{light,dark}.png`, `kaff-933-sub-form-390.png`,
  `kaff-933-sub-created.png` (create, just before submit), `kaff-933-sub-edit-dark.png`,
  `kaff-933-sub-edit-390.png`, `kaff-933-sub-confirm.png` (the archive confirm dialog).

### supplier-form (`features/suppliers/supplier-form/*`)

- Same conversion as subcontractor-form: `kaff-field` for phone/name/address/tax registration
  (editable here, unlike the subcontractor's), `kaff-button` for save and the confirm-cancel, the
  danger zone in the footer row for the same kit-gap reason.
- Stylesheet: 90 → 35 net lines added, 55 removed (`git diff --stat 82e1adb^ 82e1adb`).
- `data-testid` count: 13 before, 13 after, same names.
- Validations exercised live: created a real supplier (`01098765432` / `مورد اختباري`) via the
  create form, landed on its edit screen (code `S-10001`). Duplicate-phone warning not re-triggered
  this session, same reasoning as subcontractor-form.
- Screenshots: `kaff-933-sup-form-{light,dark}.png`, `kaff-933-sup-form-390.png`,
  `kaff-933-sup-edit.png` (dark, the footer with save at the start and bordered archive at the
  end), `kaff-933-sup-edit-390.png`.

### catalogue-import (`features/catalogue/catalogue-import/*`)

- The file picker and the result/preview tables stay structurally as they were (not form fields,
  per the story). Every button (`import submit`, `re-import preview`, `re-import confirm`,
  `re-import cancel`) now renders through `kaff-button`. No danger zone on this screen.
- The page frame collapsed from three separately bordered/shadowed bands (`.upload`, a bare result
  block, `.reimport`) into one `CatalogueForm.dc.html`-style card, with a hairline
  `border-block-start` ahead of the re-import section instead of a second bordered/background box.
- Stylesheet: 59 → 16 net lines added, 43 removed (`git diff --stat ad63325^ ad63325`).
- `data-testid` count: 22 before, 22 after, same names — `catalogue-import-submit`,
  `catalogue-reimport-preview-button`, `catalogue-reimport-confirm-button`,
  `catalogue-reimport-cancel-button` moved from a literal attribute to `kaff-button`'s `testId`
  input (same reasoning as subcontractor-form's submit button). `catalogue-reimport`'s own testid
  on the re-import section root was carried over explicitly (it is easy to drop when restructuring
  the wrapping div — checked by grep, not assumed).
- Validations exercised: none of this screen's own logic changed (no client-side parsing exists to
  validate — the server reads the file, D-136), so nothing new to exercise; the submit button's
  `[disabled]="!canSubmit()"` binding (no file chosen) was observed directly in the light/dark
  screenshots (button shown disabled with no file selected).
- Screenshots: `kaff-933-import-{light,dark}.png`, `kaff-933-import-390.png`.

### change-password (`features/auth/change-password/*`)

- All three password fields render through `kaff-field`; the confirm field's mismatch message uses
  `kaff-field`'s `errorKey` input so it replaces the hint slot rather than stacking a second
  `<span>` below it, matching every other converted screen's error treatment exactly, per the
  story's own requirement for this screen.
- The card frame now uses the shared token scale (`--space-*`, `--radius-lg`, `--elevation-1`)
  instead of bare rem literals; visually near-identical, structurally consistent with the other
  four screens.
- This screen carried zero `data-testid` before conversion (confirmed by grep) — the E2E suite
  locates its fields by `autocomplete` attribute (`tests/E2E.Tests/E2ESession.cs:63`,
  `input[autocomplete='current-password']`), which is untouched. `kaff-button`'s `testId` input
  adds one new id, `change-password-submit`; nothing was removed or renamed.
- Validations exercised live: typed mismatched new/confirm passwords through the real form and
  captured the resulting error — `auth.password.mismatch` rendering in `kaff-field`'s error slot,
  in `--color-danger`. `minLength(8)` and the `currentPassword` required check are unchanged
  (`changePasswordSchema` in `change-password-page.ts` was not touched).
- Screenshots: `kaff-933-changepw-{light,dark}.png`, `kaff-933-changepw-390.png`,
  `kaff-933-changepw-mismatch.png` (a second field-in-error image, beyond the one the gate asks for).

### Gates, measured

- `ng build --configuration production`: exit 0, zero warning lines, checked after every one of the
  five commits (`Select-String -Pattern warning` against each build log came back empty).
- `npm test` (Angular 22's `ng test` → vitest): **`Test Files 29 passed (29)`, `Tests 149 passed
  (149)`** after every one of the five commits — the count never moved, because none of these five
  screens carries its own spec file (`bab-form-page` in particular has none at all, confirmed by
  `Grep` before starting).
- `.NET`: not re-run. Nothing under `src/Api`, `src/Domain` or `src/Infrastructure` changed.

### Tooling note

`driver.mjs`'s `signed-shot` gained two small, additive interaction hooks needed to reach and prove
these screens through the headless driver rather than by assertion: `focus-blur:<selector>` (touch
a field without submitting, so a required/pattern error renders) and `click:<step>||<step>...`
(click by text/selector, or `type:<selector>::<value>` to type into a field via
`Input.insertText`, chained to reach an edit screen behind a row menu or a just-created record).
Both are backward compatible — the existing boolean `openDrawer` flag still opens the nav drawer
exactly as before.

### Not done

- The `kaff-button` bordered-danger variant gap (above) — needs an Architect/Nabil ruling before
  subcontractor-form's and supplier-form's archive buttons can go through `kaff-button` itself.
- No cancel button was added to bab-form's footer (see that screen's own note).
- Duplicate-phone warning and the retention-percent format error were not re-driven live on
  subcontractor-form/supplier-form this session (unchanged code path, not re-verified by screenshot).
- `.NET` gates were not re-run (nothing under `src/Api`/`src/Domain`/`src/Infrastructure` changed).
