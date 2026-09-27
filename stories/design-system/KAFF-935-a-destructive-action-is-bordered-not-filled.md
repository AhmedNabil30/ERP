# KAFF-935 · A destructive action is bordered, not filled

<!-- kaff id=KAFF-935 slice=design-system points=3 state=BUILT -->

**Slice:** Design system · **Epic:** Apple-grade restyle · **Points:** 3.
**Depends on:** `KAFF-901` (`kaff-button`). This story **does** change a kit component — it is the only
story that may, and it may change nothing else in the kit.
**Design:** `.design/CatalogueForm.dc.html`, the footer row: the destructive action is
`color`/`border-color` danger over a transparent background, never a filled red fill.
**Files:** `shared/kaff-button/*`, `features/subcontractors/subcontractor-form/*`,
`features/suppliers/supplier-form/*`, `features/clients/client-form/*`, `features/users/user-form/*`.

## Story

`KAFF-930` ruled that a row's destructive action is danger **text**, never a filled red button.
`CatalogueForm.dc.html` draws a form's destructive action as bordered danger. `kaff-button` implements
neither: its four variants are `primary` (filled), `danger` (filled), `secondary` (neutral border) and
`ghost` (accent text). There is no bordered-danger variant.

`KAFF-933` hit this and stopped rather than editing the kit, which was correct. It left four buttons on
`subcontractor-form` and `supplier-form` as a plain `.danger-button` — visually right, but outside
`kaff-button`, which is why `KAFF-933` cannot close. `client-form` and `user-form` went the other way
and use the filled `variant="danger"`, which the direction forbids. Three dialects for one idea.

Add the variant, then make all four screens use it.

## The ruling this story implements (D-174, Scrum Master, 2026-09-28)

Presentation only, no business rule, so it did not go to Karim or to Nabil.

| Where | Treatment |
|---|---|
| A destructive **trigger** — the button that opens the confirmation | `variant="danger-outline"`: danger text, danger border, transparent background |
| The **confirm** button inside an inline confirmation | stays `variant="danger"`, filled — it is the one moment the application should shout, and `KAFF-931` already kept it filled on every list |
| A destructive action in a **row** | unchanged: a `kaff-row-menu` item with `tone="danger"`, text only |

The distinction is deliberate: a trigger is reversible and must not dominate a form, a confirm is the
irreversible act and must.

## Acceptance criteria

**AC-935-A** `kaff-button` accepts `variant="danger-outline"`: danger-coloured text and border,
transparent background, with hover, focus-visible, disabled and `aria-busy` states matching the other
variants. Colours come from tokens — no literal.
**AC-935-B** Zero `.danger-button` class declarations remain in the application. Grep and report zero.
Every destructive trigger on the four named screens renders through `kaff-button`.
**AC-935-C** Every destructive **confirm** button still renders `variant="danger"`, filled. Name each
one you checked. A story that flattens the confirm has misread the ruling above.
**AC-935-D** Contrast of danger text on the page background meets WCAG AA at the size used. Report the
measured ratio, not an assertion.
**AC-935-E** Every `data-testid` on the four screens still exists — including
`subcontractor-archive`, `subcontractor-archive-confirm`, `supplier-archive`,
`supplier-archive-confirm`. Grep before and after, report both counts.
**AC-935-F** `ng build --configuration production` exit 0 with no warning line; `npm test` green, counts
reported from the log's own line. `kaff-button`'s own spec, if it has one, covers the new variant.
**AC-935-G** Screenshots light and dark, desktop and 390px, of all four screens, plus one image of an
inline confirmation open showing the bordered trigger and the filled confirm together.
**AC-935-H** After this lands, re-check `KAFF-933`'s `AC-933-B` and say in this story's build notes
whether it is now met. Do not move `KAFF-933`'s trailer — the Scrum Master does that.

## Not in this story

Any other kit component. Any other screen. Any change to what these forms submit or validate. Removing
the filled `danger` variant — it stays, for confirms.

## Questions for Karim

None — presentation only.

## Build notes (2026-09-28)

**`kaff-button`** (`src/Web/src/app/shared/kaff-button/`): added `variant="danger-outline"` to the
union type (`kaff-button.ts`), a class binding (`kaff-button.html`), and CSS (`kaff-button.css`) —
transparent background, `color`/`border-color: var(--color-danger)`, hover swaps to
`var(--color-surface)`. Focus, disabled and `aria-busy` states are inherited from the shared
`.kaff-button` base rule and the global `:focus-visible` in `styles.css`, same as every other
variant — no new state code needed. The filled `.variant-danger` is untouched.

**Triggers converted to `danger-outline`:**
- `client-form-page.html` line ~203: `client-archive` (was `variant="danger"`, filled).
- `user-form-page.html`: `user-deactivate` and `user-reactivate` (were `variant="danger"`, filled).
- `subcontractor-form-page.html` and `supplier-form-page.html`: the footer `.danger-button` trigger
  (`subcontractor-archive`, `supplier-archive`) now renders as `<kaff-button variant="danger-outline"
  class="footer-danger">` — `class="footer-danger"` on the host element is an established pattern
  already used by `user-form-page.html`'s `<kaff-button class="submit">`, so `margin-inline-start:
  auto` on the host still works as a flex item.

**Confirms, unchanged in effect, changed in markup:** `subcontractor-archive-confirm` and
`supplier-archive-confirm` were a plain `<button class="danger-button">` (bordered — the wrong
treatment for a confirm) and now render as `<kaff-button variant="danger">` (filled). This is a
correction, not a preservation: the old markup had the trigger/confirm dialect backwards relative to
D-174 — both were bordered. `client-archive-confirm` and `user-*-confirm-yes` already used
`variant="danger"` filled and needed no change.

**Beyond the four named files — required by AC-935-B's "zero `.danger-button` in the application",
not by the story's own Files list:** `subcontractor-list-page.html`/`.css` and
`supplier-list-page.html`/`.css` each had one more `.danger-button` — a row-level inline archive
*confirm*, same wrong-dialect bug as above (bordered where it should be filled). Converted both to
`<kaff-button variant="danger">`, added `KaffButton` to each page's `imports`, and deleted the
`.danger-button`/`.danger-button:disabled` CSS block from both stylesheets. This is presentation
only (a confirm button's fill), not the `KAFF-932` table-shell migration — that story still owns the
row geometry change. Flagging here rather than silently expanding scope: touching these two files
was not in KAFF-935's Files section, but AC-935-B is written app-wide and grep does not stop at the
four named screens.

**Grep, zero:** `grep -rn "\.danger-button" src/Web/src` → 0 matches, whole application.

**`data-testid` counts, before → after (unchanged, only `variant`/element changed, no id touched):**
- `subcontractor-form-page.html`: 15 → 15 (includes `subcontractor-archive`,
  `subcontractor-archive-confirm`).
- `supplier-form-page.html`: 13 → 13 (includes `supplier-archive`, `supplier-archive-confirm`).
- `client-form-page.html`: 15 → 15.
- `user-form-page.html`: 41 → 41.
- `subcontractor-list-page.spec.ts` / `supplier-list-page.spec.ts` still click
  `[data-testid="subcontractor-archive-confirm-SUB-0001"]` / `[data-testid="supplier-archive-confirm-
  SUP-0001"]` — those selectors match the attribute wherever it lands (now on `kaff-button`'s inner
  `<button>`), unchanged.

**Gates measured:**
- `ng build --configuration production`: exit 0, no `warning` line in the log.
- `npm test`: `Test Files 29 passed (29)` · `Tests 149 passed (149)`.
- `kaff-button` has no spec file — nothing to extend for the new variant.

**Contrast, measured (sRGB relative-luminance formula, not asserted):**
- Light: `#a52121` danger text on `#ffffff` background → **7.40:1**.
- Dark: `#e07070` danger text on `#16181a` background → **5.70:1**.
- Both clear WCAG AA (4.5:1) for normal-size text; the button label is ~14–15px, so AA is the
  applicable threshold and both pass with margin.

**Screenshots** (`.design/shots/kaff-935/`), via `run-kaff-erp`'s `signed-shot` as `owner_demo`:
`subcontractor-form-{light,dark}-{desktop,390}.png`, `supplier-form-{light,dark}-{desktop,390}.png`,
`client-form-{light,dark}-{desktop,390}.png`, `user-form-{light,dark}-{desktop,390}.png` (16 total),
plus `subcontractor-form-confirm-open.png`.

**On the combined trigger+confirm image (AC-935-G):** the archive trigger and its confirm are
mutually exclusive in the DOM — `subcontractor-form-page.html`'s `@if (isEdit() && !confirmingArchive())`
hides the trigger the instant the confirm renders, by design, on every one of these four screens. One
screenshot literally showing both live at once does not exist in this app's state machine.
`subcontractor-form-light-desktop.png` shows the trigger (bordered, `أرشفة`), and
`subcontractor-form-confirm-open.png` (same screen, same session) shows the confirm state — cancel
(secondary) and confirm (filled `أرشفة`) side by side. Reported as the two states of one flow rather
than guessed into one impossible frame.

**AC-935-H — `KAFF-933`'s `AC-933-B` re-checked:** now met. `AC-933-B` covers five screens
(`bab-form`, `subcontractor-form`, `supplier-form`, `catalogue-import`, `change-password`); the first
three named files own the archive buttons and `bab-form`/`catalogue-import`/`change-password` have no
destructive action at all (grepped, zero `variant="danger"` or `.danger-button` in any of the three —
nothing there to violate the rule). `subcontractor-form` and `supplier-form` were the only violators
(`.danger-button` outside the kit) and both now render entirely through `kaff-button`, trigger
bordered and confirm filled. Not touching `KAFF-933`'s trailer — that is the Scrum Master's call.

**Not done:** nothing scoped to this story was skipped. The list-page fix above is an addition beyond
the Files section, done because AC-935-B required it; `KAFF-932` still owns the row-geometry table-
shell migration for those same two files.
