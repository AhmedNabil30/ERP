# KAFF-911 · Restyle: catalogue form

<!-- kaff id=KAFF-911 slice=design-system points=2 state=BUILT -->

**Slice:** Design system · **Epic:** Apple-grade restyle · **Points:** 2 — a conversion, not a
feature; no money, no permission change, no new behaviour.
**Depends on:** `KAFF-900` (BUILT `76bcf00`), `KAFF-901` (BUILT `d821b37`).
**File:** `src/Web/src/app/features/catalogue/catalogue-form/catalogue-form-page.ts` + `.html` +
`.css`. Routes: `/catalogue/new`, `/catalogue/:catalogueItemId`.

## Story
As a user of the catalogue item form, I want this screen restyled to KAFF-900's tokens and KAFF-901's
`kaff-field`/`kaff-button`, so it matches `CatalogueForm.dc.html` instead of its own bespoke field markup.

**Conversion, not a feature.** Do not re-derive behaviour already covered by
`catalogue-form-page.spec.ts` (if present) or the E2E suite. `apple-erp-design` §7.3 notes this form
currently disables its submit button on invalid input, matching `client-form-page`/`user-form-page` —
**do not change that here**; it is an open, unresolved UX policy question (flagged 2026-09-10) that is
not this story's to settle, and a single divergent form would be worse than three consistent ones.

## What to do
1. Replace each field wrapper with `kaff-field`; keep every input's existing binding/validation logic.
2. Replace the save/cancel/archive buttons with `kaff-button` in its three variants
   (primary/secondary/danger), keeping the RTL action order already documented (`ux/components.md` §7).
3. Grep the file's current colour usage; report the literal count before and after (expected 0 → 0,
   but verify, do not assume).
4. Wherever `var(--color-accent)` marks something interactive (the "استخدم المقترح" suggested-price
   action, a link), change to `var(--color-interactive)`. Where it is a semantic success/valid state,
   leave it. Ambiguous cases: leave as `--color-accent`, list for Nabil.

## Acceptance criteria — `apple-erp-design` §8, applied to this screen

**AC-911-A** Zero colour literals in `catalogue-form-page.css`. Report before/after counts.
**AC-911-B** Correct in light and dark, screenshotted.
**AC-911-C** 0px horizontal overflow at 390px and correct at desktop width.
**AC-911-D** Every `<bdi>` (the item code, the money figures) still isolates.
**AC-911-E** Every `data-testid` that existed before still exists after — grep before/after, report
both counts, confirm identical set.
**AC-911-F** No new string outside i18n; no key added or removed.
**AC-911-G** `ng build` clean, `npm test`/`vitest` green, via `/run-kaff-erp`.
**AC-911-H** No new token needed beyond KAFF-900's.
**AC-911-I** Every input keeps a real `<label for>`; the submit-button-disabled-on-invalid behaviour is
unchanged (not this story's to fix).
**AC-911-J** No flex/grid child truncates without `min-inline-size: 0`.

## Not in this story
The submit-button-disabled policy question. Any change to `catalogue.api.ts`, validators, or the
suggested-price calculation. Money-input arithmetic — untouched, server-derived as already built.

## Questions for Karim
None.
