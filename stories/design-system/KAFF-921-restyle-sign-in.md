# KAFF-921 · Restyle: sign-in

<!-- kaff id=KAFF-921 slice=design-system points=1 state=BUILT -->

**Slice:** Design system · **Epic:** Apple-grade restyle · **Points:** 1 — one field, one button, no
permission or money surface at all.
**Depends on:** `KAFF-900` (BUILT `76bcf00`), `KAFF-901` (BUILT `d821b37`).
**File:** `src/Web/src/app/features/auth/sign-in/sign-in-page.ts` + `.html` + `.css`.
Route: `/sign-in`.

## Story
As anyone signing in, I want this screen restyled to KAFF-900's tokens and KAFF-901's
`kaff-field`/`kaff-button`. This screen renders with no staff chrome around it (`app.ts`'s three
session states) — it does not gain a sidebar; only its own field/button markup converts.

**Conversion, not a feature.** Do not re-derive behaviour covered by `sign-in-page` tests, and do not
touch `auth.service.ts`/`auth.api.ts`.

## What to do
1. Replace the username/password fields with `kaff-field`.
2. Replace the submit button with `kaff-button`.
3. Grep and report colour-literal count before/after.
4. `var(--color-accent)` → `var(--color-interactive)` for interactive uses only; list ambiguous cases.

## Acceptance criteria — `apple-erp-design` §8, applied to this screen

**AC-921-A** Zero colour literals in `sign-in-page.css`. Report before/after counts.
**AC-921-B** Correct in light and dark, screenshotted.
**AC-921-C** 0px horizontal overflow at 390px and correct at desktop width.
**AC-921-D** No `<bdi>` regression (none expected on this screen; confirm).
**AC-921-E** Every `data-testid` that existed before still exists after — grep before/after, report
both counts, confirm identical set.
**AC-921-F** No new string outside i18n; no key added or removed.
**AC-921-G** `ng build` clean, `npm test`/`vitest` green, via `/run-kaff-erp`.
**AC-921-H** No new token needed beyond KAFF-900's.
**AC-921-I** The password field keeps paste enabled (`apple-erp-design` §7.3: never block paste).
**AC-921-J** No flex/grid child truncates without `min-inline-size: 0`.

## Not in this story
`auth.service.ts`, `auth.api.ts`, session/redirect logic, the forced-password-change screen (not
requested in this wave).

## Questions for Karim
None.
