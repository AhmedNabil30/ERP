# KAFF-914 · Restyle: employee form

<!-- kaff id=KAFF-914 slice=design-system points=2 state=BUILT -->

**Slice:** Design system · **Epic:** Apple-grade restyle · **Points:** 2 — a conversion, not a
feature; no money, no permission change, no new behaviour.
**Depends on:** `KAFF-900` (BUILT `76bcf00`), `KAFF-901` (BUILT `d821b37`).
**File:** `src/Web/src/app/features/employees/employee-form/employee-form-page.ts` + `.html` +
`.css`. Routes: `/employees/new`, `/employees/:employeeId`.

## Story
As a user of the employee form, I want this screen restyled to KAFF-900's tokens and KAFF-901's
`kaff-field`/`kaff-button`, matching `CatalogueForm.dc.html`'s field pattern (the same wrapper shape
applies to any form, not only the catalogue one).

**Conversion, not a feature.** The submit-button-disabled-on-invalid question (`apple-erp-design`
§7.3) is open and not this story's to resolve — leave as-is if present here.

## What to do
1. Replace field wrappers with `kaff-field`, keeping bindings/validation untouched.
2. Replace buttons with `kaff-button`.
3. If the باب lookup or duplicate-phone-warning UI is on this screen, keep
   `shared/duplicate-phone-warning/` as-is — that component is not part of this restyle wave, only
   token-align it if it visibly clashes (report if so, do not silently redesign it).
4. Grep and report colour-literal count before/after.
5. `var(--color-accent)` → `var(--color-interactive)` for interactive uses only; leave semantic
   success uses; list ambiguous cases for Nabil.

## Acceptance criteria — `apple-erp-design` §8, applied to this screen

**AC-914-A** Zero colour literals in `employee-form-page.css`. Report before/after counts.
**AC-914-B** Correct in light and dark, screenshotted.
**AC-914-C** 0px horizontal overflow at 390px and correct at desktop width.
**AC-914-D** Every `<bdi>` still isolates.
**AC-914-E** Every `data-testid` that existed before still exists after — grep before/after, report
both counts, confirm identical set.
**AC-914-F** No new string outside i18n; no key added or removed.
**AC-914-G** `ng build` clean, `npm test`/`vitest` green, via `/run-kaff-erp`.
**AC-914-H** No new token needed beyond KAFF-900's.
**AC-914-I** Every input keeps a real `<label for>`.
**AC-914-J** No flex/grid child truncates without `min-inline-size: 0`.

## Not in this story
`duplicate-phone-warning`'s own redesign. Any change to `employees.api.ts`, the day-labour/باب lookup
logic, or which role can see which field.

## Questions for Karim
None.
