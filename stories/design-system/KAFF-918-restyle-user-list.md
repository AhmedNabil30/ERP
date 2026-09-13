# KAFF-918 · Restyle: user list

<!-- kaff id=KAFF-918 slice=design-system points=2 state=BUILT -->

**Slice:** Design system · **Epic:** Apple-grade restyle · **Points:** 2 — a conversion, not a
feature; no money, no permission change, no new behaviour.
**Depends on:** `KAFF-900` (BUILT `76bcf00`), `KAFF-901` (BUILT `d821b37`).
**File:** `src/Web/src/app/features/users/user-list/user-list-page.ts` + `.html` + `.css`.
Route: `/users`.

## Story
As a user of the user list — the most privileged screen in the system — I want this screen restyled
to KAFF-900's tokens and KAFF-901's `kaff-table-row`.

**Conversion, not a feature.** No permission-model change of any kind — this screen is the one
`ux/components.md` names explicitly as not to be flattened into a generic scaffold with the client
list; keep that distinction, this is styling only.

## What to do
1. Replace row markup with `kaff-table-row`.
2. Replace any active/inactive filter with `kaff-segmented-filter`.
3. Replace buttons with `kaff-button`, role/status pills with `kaff-badge`.
4. Grep and report colour-literal count before/after; `user-list-page.css` had 4 literals in
   `apple-erp-design` §2's original table, fixed 2026-09-09 — verify still 0.
5. `var(--color-accent)` → `var(--color-interactive)` for interactive uses only; list ambiguous cases.

## Acceptance criteria — `apple-erp-design` §8, applied to this screen

**AC-918-A** Zero colour literals in `user-list-page.css`. Report before/after counts.
**AC-918-B** Correct in light and dark, screenshotted.
**AC-918-C** 0px horizontal overflow at 390px and correct at desktop width.
**AC-918-D** Every `<bdi>` still isolates.
**AC-918-E** Every `data-testid` that existed before still exists after — grep before/after, report
both counts, confirm identical set.
**AC-918-F** No new string outside i18n; no key added or removed.
**AC-918-G** `ng build` clean, `npm test`/`vitest` green, via `/run-kaff-erp`.
**AC-918-H** No new token needed beyond KAFF-900's.
**AC-918-I** No control that could hide a permission-relevant field client-side is introduced.
**AC-918-J** No flex/grid child truncates without `min-inline-size: 0`.

## Not in this story
`users.api.ts`, `user-manage.guard.ts`, any role-visibility rule.

## Questions for Karim
None.
