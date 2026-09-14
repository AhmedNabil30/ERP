# KAFF-927 · Locale switch as one segmented control, not two buttons

<!-- kaff id=KAFF-927 slice=design-system points=1 state=BUILT -->

**Slice:** Design system · **Epic:** Apple-grade restyle · **Points:** 1.
**Depends on:** `KAFF-922` (BUILT, `e6487b1`) and `KAFF-923` (top bar rebuild — **not yet built as of
this writing**). Both touch `app.html`. **This story is `BLOCKED` until `KAFF-923` lands on `main` —
do not dispatch or pull it before then**, and do not run it alongside `KAFF-923`.
**Files:** `src/Web/src/app/app.html` (~line 138), `src/Web/src/app/app.css` (locale-switch rules).

## Story
Nabil, looking at the running app: the Arabic/English switch needs work. Diagnosed by direct reading
of `app.html:50-61`/current post-`KAFF-922` location (~line 138) and `app.css:49-69` — build it, do
not re-investigate.

## Finding
Two separate bordered 44px buttons, active state marked only by teal text plus a teal border. Reads
as two unrelated buttons, not one control with two states. The teal is `--color-accent` — the old
semantic colour, not the brand/interactive token this control should use.

## What to do
1. Check `kaff-segmented-filter` (`shared/kaff-segmented-filter/kaff-segmented-filter.ts`) — it is
   already generic (`KaffSegmentedFilter<T extends string = string>`, `options: SegmentedFilterOption
   <T>[]`, `value: model.required<T>()`). **Reuse it for the locale switch rather than writing a
   second segmented control** (`ux/components.md`'s "one of each" rule) — confirmed reusable, this is
   not a "check if possible," it's a "do it."
2. Replace the two `<button class="locale-button">` elements and the wrapping `<nav
   class="locale-switch">` with `<kaff-segmented-filter [options]="localeOptions" [value]="i18n.locale
   ()" (valueChange)="switchLocale($event)" ariaLabelKey="app.language" testIdPrefix="locale" />` (or
   the equivalent binding shape `app.ts`'s current `switchLocale`/`locales`/`i18n.locale()` already
   supports — adapt to the real signatures, don't assume these exact names without checking `app.ts`).
3. Active segment filled `--color-interactive` with white/on-interactive text — same token
   `KAFF-900` already defines for the active filter segment. `--color-accent` must not appear in this
   control after the change.
4. Compact height matching the filter's existing size (`kaff-segmented-filter.css` already sets it —
   don't override it per-caller unless the top bar's vertical rhythm genuinely requires a variant; if
   it does, say why).
5. `aria-pressed` semantics preserved — `kaff-segmented-filter`'s existing markup should already carry
   the right ARIA for a toggle group; verify it does (check `kaff-segmented-filter.html`) rather than
   assuming.

## Acceptance criteria
**AC-927-A** One bordered track, two segments, active segment filled `--color-interactive`, not two
independent bordered buttons.
**AC-927-B** `--color-accent` does not appear in the locale switch after this change.
**AC-927-C** Locale actually switches on click — behaviour unchanged, only the control's markup/CSS
changed. Verify by clicking both segments in the rendered app, not by reading the binding.
**AC-927-D** Existing `data-testid`s for the locale switch (whatever they currently are — grep before
changing) still resolve to something, even if the exact testid string changes because the DOM
structure changed to `kaff-segmented-filter`'s own testid pattern — if a testid must change, say so
explicitly and check nothing else in the codebase (an E2E spec) depends on the old one.
**AC-927-E** Zero colour literals; screenshot light+dark, desktop+390px, actually rendered.
**AC-927-F** `ng build` clean, `npm test` green, `BidiGeometryTests` green before commit (D-156).

## Not in this story
Any other part of the top bar — that's `KAFF-923`, already built or in progress by the time this
starts.

## Questions for Karim
None.
