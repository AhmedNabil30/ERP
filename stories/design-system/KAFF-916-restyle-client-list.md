# KAFF-916 · Restyle: client list

<!-- kaff id=KAFF-916 slice=design-system points=2 state=BUILT -->

**Slice:** Design system · **Epic:** Apple-grade restyle · **Points:** 2 — a conversion, not a
feature; no money, no permission change, no new behaviour.
**Depends on:** `KAFF-900` (BUILT `76bcf00`), `KAFF-901` (BUILT `d821b37`).
**File:** `src/Web/src/app/features/clients/client-list/client-list-page.ts` + `.html` + `.css`.
Route: `/clients`.

## Story
As a user of the client list, I want this screen restyled to KAFF-900's tokens and KAFF-901's
`kaff-table-row`, ending the current card-per-row pattern `apple-erp-design` §3 names as the
example of what makes forty clients feel like forty documents.

**Conversion, not a feature.** Do not re-derive behaviour covered by `client-list-page` tests.

## What to do
1. Replace the card-per-row markup with `kaff-table-row` — this is the exact defect `apple-erp-design`
   §3 describes ("the current list screens draw a card per row"); converting it is the point of this
   story, not an optional extra.
2. Replace any active/archived/all filter with `kaff-segmented-filter`.
3. Replace buttons with `kaff-button`.
4. Grep and report colour-literal count before/after; this file appeared in `apple-erp-design` §2's
   original defect table (`client-list-page.css: 8 literals`, fixed 2026-09-09) — verify it is still 0
   after this conversion, do not assume the earlier fix survives untouched.
5. `var(--color-accent)` → `var(--color-interactive)` for interactive uses only; list ambiguous cases.

## Acceptance criteria — `apple-erp-design` §8, applied to this screen

**AC-916-A** Zero colour literals in `client-list-page.css`. Report before/after counts.
**AC-916-B** Correct in light and dark, screenshotted.
**AC-916-C** 0px horizontal overflow at 390px and correct at desktop width.
**AC-916-D** Every `<bdi>` (client codes, phones) still isolates, and `justify-self: start` on
`.row-link > bdi` (the grid-child stretch fix, `apple-erp-design` §1) is preserved if this file has it.
**AC-916-E** Every `data-testid` that existed before still exists after — grep before/after, report
both counts, confirm identical set.
**AC-916-F** No new string outside i18n; no key added or removed.
**AC-916-G** `ng build` clean, `npm test`/`vitest` green, via `/run-kaff-erp`.
**AC-916-H** No new token needed beyond KAFF-900's.
**AC-916-I** No card-per-row remains; rows are hairline-separated.
**AC-916-J** No flex/grid child truncates without `min-inline-size: 0`.

## Not in this story
The duplicate-phone-warning dialog (§13 of `ux/components.md`), `clients.api.ts`, or the client-create
flow's business rules.

## Questions for Karim
None.
