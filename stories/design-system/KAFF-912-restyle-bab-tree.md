# KAFF-912 · Restyle: باب tree

<!-- kaff id=KAFF-912 slice=design-system points=2 state=BUILT -->

**Slice:** Design system · **Epic:** Apple-grade restyle · **Points:** 2 — a conversion, not a
feature; no money, no permission change, no new behaviour.
**Depends on:** `KAFF-900` (BUILT `76bcf00`), `KAFF-901` (BUILT `d821b37`).
**File:** `src/Web/src/app/features/babs/bab-tree/bab-tree-page.ts` + `.html` + `.css`.
Route: `/babs`.

## Story
As a user of the باب tree, I want this screen restyled to KAFF-900's tokens and KAFF-901's
`kaff-table-row`/`kaff-badge`, so it matches `BabTree.dc.html`'s hairline-dense list.

**Conversion, not a feature.** `bab-tree-page`'s existing tree/nesting logic (`bab-tree.ts` in
`core/catalogue/`) is untouched — this is presentation only. Do not re-derive behaviour covered by
existing tests.

## What to do
1. Replace each row (parent باب and nested child رow) with `kaff-table-row`, keeping the existing
   indent/hierarchy markup for nesting — `kaff-table-row` styles one row; nesting indentation stays
   the screen's own concern per `ux/components.md` §8 (identity-first, actions-last is the row's job;
   tree indent is the caller's).
2. Replace the margin-percentage figure and the "مؤرشف" chip with `kaff-badge`.
3. Replace the "باب جديد" / "تعديل" / "أرشفة" actions with `kaff-button`.
4. Grep and report colour-literal count before/after.
5. `var(--color-accent)` → `var(--color-interactive)` for interactive uses only (buttons, the active
   row indicator); leave semantic success uses as `--color-accent`; list ambiguous cases for Nabil.

## Acceptance criteria — `apple-erp-design` §8, applied to this screen

**AC-912-A** Zero colour literals in `bab-tree-page.css`. Report before/after counts.
**AC-912-B** Correct in light and dark, screenshotted.
**AC-912-C** 0px horizontal overflow at 390px and correct at desktop width.
**AC-912-D** Every `<bdi>` (باب codes) still isolates.
**AC-912-E** Every `data-testid` that existed before still exists after — grep before/after, report
both counts, confirm identical set.
**AC-912-F** No new string outside i18n; no key added or removed.
**AC-912-G** `ng build` clean, `npm test`/`vitest` green, via `/run-kaff-erp`.
**AC-912-H** No new token needed beyond KAFF-900's.
**AC-912-I** The "cannot archive a باب holding active items" message keeps its `role="alert"` if it
has one today; icon buttons keep `aria-label`.
**AC-912-J** No flex/grid child truncates without `min-inline-size: 0`.

## Not in this story
The باب create/edit form (`bab-form-page`) — not in Nabil's requested list for this wave, named as
skipped in the batch report. Any change to `core/catalogue/bab-tree.ts`'s tree-building logic or the
cycle guard (D-127).

## Questions for Karim
None.
