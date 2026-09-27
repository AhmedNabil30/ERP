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
