# KAFF-919 · Restyle: user form

<!-- kaff id=KAFF-919 slice=design-system points=2 state=BUILT -->

**Slice:** Design system · **Epic:** Apple-grade restyle · **Points:** 2 — a conversion, not a
feature; no money, no permission change, no new behaviour.
**Depends on:** `KAFF-900` (BUILT `76bcf00`), `KAFF-901` (BUILT `d821b37`).
**File:** `src/Web/src/app/features/users/user-form/user-form-page.ts` + `.html` + `.css`.
Routes: `/users/new`, `/users/:userId`.

## Story
As a user of the user form, I want this screen restyled to KAFF-900's tokens and KAFF-901's
`kaff-field`/`kaff-button`.

**Conversion, not a feature.** The submit-button-disabled-on-invalid question (`apple-erp-design`
§7.3) is open and not this story's to resolve — this screen is the other of the two named examples;
leave that behaviour exactly as it is.

## What to do
1. Replace field wrappers with `kaff-field`.
2. Replace buttons with `kaff-button`.
3. Grep and report colour-literal count before/after; `user-form-page.css` had 12 literals in
   `apple-erp-design` §2's original table, fixed 2026-09-09 — verify still 0.
4. `var(--color-accent)` → `var(--color-interactive)` for interactive uses only; list ambiguous cases.

## Acceptance criteria — `apple-erp-design` §8, applied to this screen

**AC-919-A** Zero colour literals in `user-form-page.css`. Report before/after counts.
**AC-919-B** Correct in light and dark, screenshotted.
**AC-919-C** 0px horizontal overflow at 390px and correct at desktop width.
**AC-919-D** Every `<bdi>` still isolates.
**AC-919-E** Every `data-testid` that existed before still exists after — grep before/after, report
both counts, confirm identical set.
**AC-919-F** No new string outside i18n; no key added or removed.
**AC-919-G** `ng build` clean, `npm test`/`vitest` green, via `/run-kaff-erp`.
**AC-919-H** No new token needed beyond KAFF-900's.
**AC-919-I** Every input keeps a real `<label for>`; submit-disabled-on-invalid unchanged.
**AC-919-J** No flex/grid child truncates without `min-inline-size: 0`.

## Not in this story
`users.api.ts`, role/department validation, temporary-password mechanics, submit-disabled policy.

## Questions for Karim
None.
