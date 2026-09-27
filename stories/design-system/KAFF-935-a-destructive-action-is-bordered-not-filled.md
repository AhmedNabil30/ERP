# KAFF-935 · A destructive action is bordered, not filled

<!-- kaff id=KAFF-935 slice=design-system points=3 state=READY -->

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
