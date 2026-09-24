# KAFF-930 · The interaction kit — row menu, detail panel, inline edit, states

<!-- kaff id=KAFF-930 slice=design-system points=8 state=READY -->

**Slice:** Design system · **Epic:** Apple-grade restyle · **Points:** 8.
**Depends on:** `KAFF-928` (`kaff-table` + `--kaff-columns`), `KAFF-929` (tokens, Plex, top bar).
**Design:** `docs/superpowers/specs/2026-09-21-shell-and-interaction-redesign-design.md` §5, and the
canvas at https://claude.ai/artifact/KmuT1UYyXMiSqocN2MCkgY — artboards `Catalogue`, `BabTree`,
`States`.
**Files:** new `src/Web/src/app/shared/kaff-row-menu/*`, `kaff-detail-panel/*`, `kaff-inline-edit/*`,
`kaff-state/*`; changed `src/Web/src/styles.css` if a token is missing.
**This story builds the components only.** `KAFF-931` puts them on the screens.

## Story

Every list screen in this app currently hand-rolls the same four things: a band of buttons per row,
a separate form page for a one-field edit, its own loading and empty markup, and filter state that
lives only in a signal. The screens Nabil called a disaster are the ones where that shows worst —
`bab-tree-page` renders three buttons and one red `أرشفة الباب` per row, eight times down the page.

Build the four shared pieces once. `KAFF-931` then deletes the per-screen copies.

## What `ux/components.md` rules out, and why this story is smaller than the design doc

The design doc §5 lists six components. Two are not built here:

- **No toast or snackbar.** `ux/components.md` §"deliberately not in this inventory": *"a toast /
  snackbar system … an inline confirmation on the screen is enough, and toasts that carry errors are
  how errors get missed."* `CLAUDE.md` makes `ux/components.md` the authority on component shape.
  **Archive keeps the inline confirmation `catalogue-list-page` already implements.** The
  undo-instead-of-confirm idea in the design doc and on the `States` artboard is a **UX policy change
  that needs Nabil or the BA**, not a component a builder decides. Raised, not built.
- **No bulk selection bar.** `ux/components.md`: *"Selection — only if a feature needs bulk actions
  … Do not build it."* No existing screen needs it. It comes back with a feature that does.

## The four components

### 1 · `kaff-row-menu`

The `⋯` at the end of a row. Replaces every per-row button band.

- Input `items: readonly RowMenuItem[]`, where an item is `{ labelKey, action, tone?: 'default' | 'danger', disabled? }`.
- Input `triggerLabelKey` — the trigger is icon-only, so it **must** carry an `aria-label`
  (`apple-erp-design` §7.1). No key, no build.
- The trigger is **visible on hover and on `:focus-visible`**, and is always in the tab order.
  `visibility`/`opacity`, never `display: none` — a control that vanishes from the accessibility tree
  is not reachable by keyboard, which is the trap this rule exists to stop.
- Open on click or `Enter`/`Space`; close on `Escape`, on outside click, on route change. Focus
  returns to the trigger on close.
- Menu is `role="menu"`, items `role="menuitem"`, arrow keys move between them.
- A `danger` item is `--color-danger` **text on the normal surface** — never a filled red button.
- 44px minimum per item (`--tap-target`).

### 2 · `kaff-detail-panel`

The surface a row opens for a quick read or edit. Not a page, not a modal.

- Inputs: `open: boolean`, `titleKey` or `title: string`, `testId`.
- Outputs: `closed`.
- Renders at the inline-end of the list, `--color-surface`, one hairline border, `--elevation-1`.
  **At `width < 48rem` it is a full-height sheet** with `overscroll-behavior: contain`.
- Projects three slots: header meta, body, footer actions.
- `Escape` closes. Focus moves into the panel on open and returns to the row on close.
  `aria-labelledby` points at its own heading.
- **It does not own the URL.** The caller puts the open record in a query param (§4 below) so the
  panel is linkable and survives a refresh.

### 3 · `kaff-inline-edit`

One scalar, edited in place: a price, a margin %, a unit, a name.

- Inputs: `value`, `type: 'text' | 'decimal'`, `ariaLabelKey`, `disabled`.
- Outputs: `committed(value)`, `cancelled`.
- `Enter` commits, `Escape` reverts, blur commits. Optimistic: show the new value, and **roll back
  and render the server's refusal** if the call fails.
- `inputmode="decimal"` for numbers, `tabular-nums`, `translate="no"` on codes.
- ⛔ **Never used for anything that changes what money is legal** — posting amounts, extract
  quantities, hold percentages. Those go through a full page with an approval chain behind them.
  A reviewer seeing `kaff-inline-edit` on such a field should reject the story.

### 4 · `kaff-state`

One component, four modes, replacing the per-screen copies.

- Input `mode: 'loading' | 'empty' | 'filtered-empty' | 'refusal'`, plus `titleKey`, `bodyKey`,
  and a projected action slot.
- `loading` renders a **skeleton in the shape of the rows**, not a spinner in a void, and carries
  `role="status" aria-live="polite"`.
- `refusal` is `role="alert"` and shows the server's translated message. It never invents its own.
- `filtered-empty` is a different fact from `empty` and says how many rows the filter is hiding —
  telling an operator "no clients" when they mistyped a number is how a duplicate gets created.
- Matches the `States` artboard, panels 1–5.

## 4 · URL state — a pattern, not a component

Filters, search text, the open panel's record and the active tab move into query params, using the
`ActivatedRoute` / `queryParamsHandling: 'merge'` shape `catalogue-list-page.ts` already uses. Today
they live only in signals: the state cannot be linked, bookmarked, or restored by a refresh, and an
operator who opens a row and comes back has lost their filter. Document the pattern in this story's
build notes; `KAFF-931` applies it per screen.

## Acceptance criteria

**AC-930-A** Four components exist under `src/Web/src/app/shared/`, standalone, `OnPush`, signals,
`inject()`, new control flow — no NgModule, no `BehaviorSubject`, no `*ngIf`.
**AC-930-B** `kaff-row-menu`'s trigger is reachable and operable by keyboard alone, proven by a
`.spec.ts` that tabs to it and opens it with `Enter`. It is not `display: none` when unhovered.
**AC-930-C** `kaff-detail-panel` traps nothing but returns focus to its opener on `Escape`, proven by
a spec. At 390px it renders as a full-height sheet.
**AC-930-D** `kaff-inline-edit` commits on `Enter`, reverts on `Escape`, and rolls back on a rejected
commit, proven by a spec.
**AC-930-E** `kaff-state` renders all four modes; `loading` is a row-shaped skeleton with
`aria-live="polite"`; `refusal` is `role="alert"`.
**AC-930-F** No toast, no snackbar, no bulk-selection bar anywhere in the diff.
**AC-930-G** Zero colour literals; every colour a token. Any new token declared in `:root` **and** in
the dark block.
**AC-930-H** No hardcoded strings; every new key in both `ar.json` and `en.json`.
**AC-930-I** `ng build` clean, `npm test` green.
**AC-930-J** A dev-fixture route renders all four components together, light and dark, desktop and
390px, screenshotted through `/run-kaff-erp` — the components have no product screen yet, so the
fixture is the proof.

## Not in this story

Applying any of this to a product screen — that is `KAFF-931`. The undo-vs-confirm policy. Bulk
selection. Anything under `src/Api/`.

## Questions for Nabil

**Undo or confirm for archive?** The design doc and the `States` artboard propose archiving
immediately with an 8-second undo. `ux/components.md` forbids the toast that would carry it and
prefers the inline confirmation already shipped. This story builds the inline confirmation. Changing
that is a UX policy decision, and it is yours.
