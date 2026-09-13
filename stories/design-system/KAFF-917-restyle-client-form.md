# KAFF-917 · Restyle: client form

<!-- kaff id=KAFF-917 slice=design-system points=2 state=BUILT -->

**Slice:** Design system · **Epic:** Apple-grade restyle · **Points:** 2 — a conversion, not a
feature; no money, no permission change, no new behaviour.
**Depends on:** `KAFF-900` (BUILT `76bcf00`), `KAFF-901` (BUILT `d821b37`).
**File:** `src/Web/src/app/features/clients/client-form/client-form-page.ts` + `.html` + `.css`.
Routes: `/clients/new`, `/clients/:clientId`.

## Story
As a user of the client form, I want this screen restyled to KAFF-900's tokens and KAFF-901's
`kaff-field`/`kaff-button`.

**Conversion, not a feature.** The submit-button-disabled-on-invalid question (`apple-erp-design`
§7.3) is open and not this story's to resolve — this screen is one of the two named examples; leave
that behaviour exactly as it is. The duplicate-phone-warning dialog (`ux/components.md` §13) is
**not** in scope for this restyle wave — it is its own component, already built; only re-token it if
KAFF-900's tokens make it visually clash, and report that rather than redesigning it.

## What to do
1. Replace field wrappers with `kaff-field`.
2. Replace buttons with `kaff-button`, keeping the RTL action order.
3. Grep and report colour-literal count before/after — `client-form-page.css` had 10 literals in
   `apple-erp-design` §2's original table, fixed 2026-09-09; verify it is still 0 today.
4. `var(--color-accent)` → `var(--color-interactive)` for interactive uses only; list ambiguous cases.

## Acceptance criteria — `apple-erp-design` §8, applied to this screen

**AC-917-A** Zero colour literals in `client-form-page.css`. Report before/after counts.
**AC-917-B** Correct in light and dark, screenshotted.
**AC-917-C** 0px horizontal overflow at 390px and correct at desktop width.
**AC-917-D** Every `<bdi>` (phone, code) still isolates.
**AC-917-E** Every `data-testid` that existed before still exists after — grep before/after, report
both counts, confirm identical set.
**AC-917-F** No new string outside i18n; no key added or removed.
**AC-917-G** `ng build` clean, `npm test`/`vitest` green, via `/run-kaff-erp`.
**AC-917-H** No new token needed beyond KAFF-900's.
**AC-917-I** Every input keeps a real `<label for>`; submit-disabled-on-invalid unchanged.
**AC-917-J** No flex/grid child truncates without `min-inline-size: 0`.

## Not in this story
`duplicate-phone-warning`'s own redesign, `clients.api.ts`, phone normalisation, submit-disabled
policy.

## Questions for Karim
None.
