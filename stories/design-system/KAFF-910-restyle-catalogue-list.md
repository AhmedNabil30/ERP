# KAFF-910 · Restyle: catalogue list

<!-- kaff id=KAFF-910 slice=design-system points=2 state=BUILT -->

**Slice:** Design system · **Epic:** Apple-grade restyle · **Points:** 2 — a conversion, not a
feature; no money, no permission change, no new behaviour.
**Depends on:** `KAFF-900` (BUILT `76bcf00`), `KAFF-901` (BUILT `d821b37`).
**File:** `src/Web/src/app/features/catalogue/catalogue-list/catalogue-list-page.ts` +
`.html` + `.css`. Route: `/catalogue` (`app.routes.ts`).

## Story
As a user of the catalogue list, I want this screen restyled to KAFF-900's tokens and KAFF-901's
shared components, so it matches `Main.dc.html` instead of carrying its own bespoke row/filter markup.

**This is a conversion, not a new feature.** Do not re-derive Given/When/Then for behaviour that
already shipped and already has tests (`catalogue-list-page.spec.ts`). The acceptance criteria below
are the `apple-erp-design` §8 checklist plus "nothing behavioural moved."

## What to do
1. Replace this screen's row markup with `kaff-table-row` (`src/Web/src/app/shared/kaff-table-row/`).
2. Replace its active/archived/all filter with `kaff-segmented-filter`.
3. Replace any bespoke button with `kaff-button`, any status/margin pill with `kaff-badge`.
4. Grep the file's current colour usage first. Feature stylesheets are already at 0 hex/`rgb()`
   literals repo-wide (`apple-erp-design` §2 was fixed 2026-09-09) — **report the count, do not assume
   it's already zero without checking this file specifically.**
5. Wherever `var(--color-accent)` is used for something interactive here — a button, a link, an active
   state — change it to `var(--color-interactive)` (KAFF-900). Wherever it means a semantic success/
   positive state, leave it as `--color-accent`. **Do not merge the two tokens.** If a usage is
   ambiguous, leave it as `--color-accent` and list it — that call is Nabil's, not yours.

## Acceptance criteria — `apple-erp-design` §8, applied to this screen

**AC-910-A** Zero colour literals in `catalogue-list-page.css`. Report the count before and after.
**AC-910-B** Correct in both light and dark, screenshotted, not reasoned about.
**AC-910-C** 0px horizontal overflow at 390px, and correct at desktop width.
**AC-910-D** Every `<bdi>` still isolates; no grid-child `<bdi>` stretches.
**AC-910-E** Every `data-testid` that existed before this change still exists after it — grep the file
before and after, report both counts and confirm the set is identical.
**AC-910-F** No new string outside i18n; no i18n key added or removed.
**AC-910-G** `ng build` clean, `npm test`/`vitest` green — run through `/run-kaff-erp`.
**AC-910-H** Any new token this screen needs is already in `:root` from KAFF-900; this story adds none.
**AC-910-I** Icon buttons keep their `aria-label`; the one `<h1>` on the page is unchanged.
**AC-910-J** No flex/grid child truncates without `min-inline-size: 0`.

## Not in this story
Any new field, any new endpoint call, any change to `catalogue.api.ts` or the guard. This story is
`.html`/`.css`/template-only, converting existing markup onto existing components.

## Questions for Karim
None.
