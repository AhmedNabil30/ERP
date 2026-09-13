# KAFF-920 · Restyle: audit trail

<!-- kaff id=KAFF-920 slice=design-system points=2 state=BUILT -->

**Slice:** Design system · **Epic:** Apple-grade restyle · **Points:** 2 — a conversion, not a
feature; no money, no permission change, no new behaviour.
**Depends on:** `KAFF-900` (BUILT `76bcf00`), `KAFF-901` (BUILT `d821b37`).
**File:** `src/Web/src/app/features/audit/audit-trail-page.ts` + `.html` + `.css`.
Route: `/audit`.

## Story
As the Owner viewing the audit trail, I want this screen restyled to KAFF-900's tokens and KAFF-901's
`kaff-table-row`, keeping its cursor-based paging (`ux/components.md` §8: "Paging: cursor-based
(`action.load_more`) for anything append-only, like the audit trail") exactly as it is.

**Conversion, not a feature.** No change to what a non-Owner sees (nothing — D-049 ruling 1, no
project filter, ever). Do not re-derive behaviour covered by existing tests.

## What to do
1. Replace row markup with `kaff-table-row`.
2. Replace the "load more" action with `kaff-button` if it is a button today.
3. Grep and report colour-literal count before/after; `audit-trail-page.css` had 9 literals and 1
   token in `apple-erp-design` §2's original table, fixed 2026-09-09 — verify still 0.
4. `var(--color-accent)` → `var(--color-interactive)` for interactive uses only; list ambiguous cases.

## Acceptance criteria — `apple-erp-design` §8, applied to this screen

**AC-920-A** Zero colour literals in `audit-trail-page.css`. Report before/after counts.
**AC-920-B** Correct in light and dark, screenshotted.
**AC-920-C** 0px horizontal overflow at 390px and correct at desktop width.
**AC-920-D** Every `<bdi>` (actor names, entity ids) still isolates.
**AC-920-E** Every `data-testid` that existed before still exists after — grep before/after, report
both counts, confirm identical set.
**AC-920-F** No new string outside i18n; no key added or removed.
**AC-920-G** `ng build` clean, `npm test`/`vitest` green, via `/run-kaff-erp`.
**AC-920-H** No new token needed beyond KAFF-900's.
**AC-920-I** Cursor-based `action.load_more` paging behaviour unchanged.
**AC-920-J** No flex/grid child truncates without `min-inline-size: 0`.

## Not in this story
`audit.api.ts`, the no-project-filter rule, retention (`KAFF-129`).

## Questions for Karim
None.
