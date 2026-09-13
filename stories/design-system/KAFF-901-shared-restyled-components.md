# KAFF-901 · Shared restyled components: table row, segmented filter, form field, button, badge

<!-- kaff id=KAFF-901 slice=design-system points=5 state=BUILT -->

**Slice:** Design system · **Epic:** Apple-grade restyle · **Points:** 5.
**Source:** same four mockups as `KAFF-900` · `.claude/skills/apple-erp-design/SKILL.md` §3, §4, §8 ·
`ux/components.md` (component shape and prohibitions — this file wins on how a component is built).
**Depends on:** `KAFF-900` — BUILT at `76bcf00`, 2026-09-13. Unblocked.

## Story
As Frontend, I want one restyled implementation of each shared visual piece the mockups use
repeatedly, so every screen story that follows converts against a finished component instead of
re-deriving the same CSS five times.

## What ships — one component each, under `src/Web/src/app/shared/`

None of these exist as shared components yet (checked: `src/Web/src/app/shared/` today holds only
`phone-match.ts` and `duplicate-phone-warning/`). This story creates them for the first time, styled
to the mockups, following the Angular 22 shape in `ux/components.md` §"The Angular 22 shape".

1. **Table row / hairline table** (`Main.dc.html`'s catalogue table, `BabTree.dc.html`'s باب list).
   Dense grid row, 1px `--color-border` separator between rows, no card-per-row, `.figure` class
   (already in `styles.css`) on every numeric cell, identity column first (inline-start), actions last
   (inline-end) — `ux/components.md` §8's rules, now actually built rather than only documented.
2. **Segmented filter** (`Main.dc.html`'s نشط/مؤرشف/الكل toolbar control). One bordered track, active
   segment filled with `--color-interactive`. Wraps the existing `.chip`/`aria-pressed` markup per
   `apple-erp-design` §4 — do not invent a second filter primitive if `.chip` already does the job.
3. **Form field** (`kaff-field` — `ux/components.md` §1, `CatalogueForm.dc.html`'s field pattern).
   Label above control, hint/error below, `aria-describedby` wired, required marker `*` plus
   `a11y.required`. This is the first time this component is actually built; several existing forms
   (`client-form-page`, `user-form-page`, `catalogue-form-page`) currently roll their own field markup
   — **this story does not migrate them**, it only builds the shared component. Migration is each
   screen's own story.
4. **Button** (`kaff-button` — `ux/components.md` §7). `primary`/`secondary`/`danger`/`ghost` variants,
   44px tap target, RTL action order (primary at inline-start, DOM order not `row-reverse`), `busy`
   state keeps label visible.
5. **Badge** (the margin-percentage pill in `Main.dc.html`/`BabTree.dc.html`, and the "مؤرشف" chip).
   Neutral/accent/warning tones from tokens, `--radius-sm` per `apple-erp-design` §3 for inline badges
   (pills at `999px` are filter chips only, per the same section — do not conflate the two shapes).

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | One of each — no second form field, no second button | `ux/components.md` header |
| 2 | Standalone, signals, `OnPush`, `inject()`, `input()`/`output()`/`model()` | `ux/components.md` §"Angular 22 shape" |
| 3 | Zero colour literals; every colour from `KAFF-900`'s tokens | `apple-erp-design` §2, §8 |
| 4 | 44px tap target on every interactive piece | `ux/components.md` §"Tap targets" |
| 5 | Colour never the only signal of state (archived, active, error) | `apple-erp-design` §3 |

## Acceptance criteria

**AC-901-A — components exist, one each**
Given `src/Web/src/app/shared/`
When this story is built
Then exactly one implementation each of the table row, segmented filter, form field, button and badge
exists, none of them feature-owned.

**AC-901-B — no NgModule, no legacy Angular idiom**
Given any new component file
When reviewed
Then it uses `input()`/`output()`/`model()`, `inject()`, `@if`/`@for` with `track`, and carries no
`@Input()`/`@Output()`/`EventEmitter`/`*ngIf`/`*ngFor`/constructor injection.

**AC-901-C — accessibility baseline**
Given the button and form field
When rendered
Then every input has a real `<label for>`, icon-only buttons carry `aria-label`, and
`:focus-visible` is not overridden away.

**AC-901-D — dark mode**
Given each component
When rendered under `prefers-color-scheme: dark`
Then every surface, border and text colour resolves from a token with a dark-mode value, screenshotted
via `driver.mjs shot`.

**AC-901-E — no colour literal**
Given the five new stylesheets
When `apple-erp-design` §8's checklist is run
Then zero colour literals are present.

## Not in this story
Migrating any existing screen to use these components — that is each screen's own story, sequenced
after this one. Selection/bulk actions on the table row (`ux/components.md` §8: "Slice 1 does not
[need it]. Do not build it" — same reasoning applies here until a screen needs it).

## Questions for Karim
None.
