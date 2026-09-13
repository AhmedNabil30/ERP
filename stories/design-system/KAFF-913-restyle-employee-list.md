# KAFF-913 · Restyle: employee list

<!-- kaff id=KAFF-913 slice=design-system points=2 state=BUILT -->

**Slice:** Design system · **Epic:** Apple-grade restyle · **Points:** 2 — a conversion, not a
feature; no money, no permission change, no new behaviour.
**Depends on:** `KAFF-900` (BUILT `76bcf00`), `KAFF-901` (BUILT `d821b37`).
**File:** `src/Web/src/app/features/employees/employee-list/employee-list-page.ts` + `.html` +
`.css`. Route: `/employees`.

## Story
As a user of the employee list, I want this screen restyled to KAFF-900's tokens and KAFF-901's
shared components, matching the `Main.dc.html` list pattern.

**Conversion, not a feature.** Do not re-derive behaviour already covered by existing tests.

## What to do
1. Replace row markup with `kaff-table-row`.
2. Replace any status/kind filter with `kaff-segmented-filter` if the screen has one; if it uses a
   different control today, convert it and name the change.
3. Replace buttons with `kaff-button`, any kind/status pill with `kaff-badge`.
4. Grep and report colour-literal count before/after.
5. `var(--color-accent)` → `var(--color-interactive)` for interactive uses only; leave semantic
   success uses; list ambiguous cases for Nabil.

## Acceptance criteria — `apple-erp-design` §8, applied to this screen

**AC-913-A** Zero colour literals in `employee-list-page.css`. Report before/after counts.
**AC-913-B** Correct in light and dark, screenshotted.
**AC-913-C** 0px horizontal overflow at 390px and correct at desktop width.
**AC-913-D** Every `<bdi>` (employee codes, phones) still isolates.
**AC-913-E** Every `data-testid` that existed before still exists after — grep before/after, report
both counts, confirm identical set.
**AC-913-F** No new string outside i18n; no key added or removed.
**AC-913-G** `ng build` clean, `npm test`/`vitest` green, via `/run-kaff-erp`.
**AC-913-H** No new token needed beyond KAFF-900's.
**AC-913-I** Day rate / salary figures stay wherever the permission model already restricts them —
this story does not touch what is rendered to which role, only how it looks.
**AC-913-J** No flex/grid child truncates without `min-inline-size: 0`.

## Not in this story
Any change to `employees.api.ts`, guards, or which fields are visible to which role.

## Questions for Karim
None.
