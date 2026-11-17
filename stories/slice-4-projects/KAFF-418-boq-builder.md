# KAFF-418 · The BOQ builder: start from a prepared base or an earlier project, add many lines, sign

<!-- kaff id=KAFF-418 slice=4 points=13 state=VERIFIED verdict=CONDITIONAL at=91cdcb3 on=2026-11-17 -->

**Slice:** 4 — Projects · **Points:** 13.
**Owner:** Backend, then UI Designer (`build` from `.design/v2/team-a/boq.html`, commit `9a61abf`, with the D-218 changes).
**Source:** Nabil's rulings D-217 items 19 to 33 and D-218 items 10 to 13. Journey brief 3 in
`ux/journey-project-module-2026-11-09.md`.
**Spec:** `spec.md` §4.4 (freeze by copy), §4.5 (custom items), §5.1.

## Acceptance criteria

| ID | Criterion | Source |
|---|---|---|
| AC-1 | The technical office can prepare a named base BOQ: a name and lines (catalogue item and quantity). A base holds no rates, no hold and no advance. | D-217 items 19, 20 |
| AC-2 | Everyone who may build a BOQ can list the prepared bases. | D-218 item 13 |
| AC-3 | An empty BOQ can be filled from a prepared base or from an earlier project the user can open. Items and quantities come across and stay editable. Rates are the catalogue rates of today. Hold and advance percentages do not come across. | D-217 items 19, 20 |
| AC-4 | Several catalogue items can be added to the BOQ in one request. | D-217 "what this needs" |
| AC-5 | A line has one quantity. It counts as estimated until a survey is recorded; the surveyed value then replaces it. | D-217 item 21 |
| AC-6 | A BOQ with a line that has no quantity cannot be signed; the refusal names the line. | D-217 item 23 |
| AC-7 | A custom item can be typed from the builder. It is a normal line: it can be changed and removed like any other. It is stored flagged `pendingCatalogueReview` for the Technical Office queue and writes nothing to the catalogue. | D-217 item 26, D-218 item 12, `spec.md` §4.5 |
| AC-8 | A BOQ can be signed while a custom item waits for review. | D-218 item 11 (Q-218-2) |
| AC-9 | Hold and advance percentages are required at signature and start empty. | D-217 item 29 |
| AC-10 | The BOQ of an archived project is read-only. | D-217 item 31 |
| AC-11 | The empty BOQ shows one main button, "ابدأ المقايسة", opening one sheet: blank BOQ first, then prepared bases, then earlier projects. | D-218 item 10 |
| AC-12 | A line is removed without a confirmation. The rate shows on every line. No control for site conditions or line markup. | D-217 items 24, 25, 27 |
| AC-13 | The act is named "توقيع المقايسة" on the button, the sheet title and the confirm. The printed paper is titled "عرض سعر". | D-217 items 28, 33 |
| AC-14 | The BOQ line shows no review mark for a custom item. | D-218 item 12, a reading by the coordinator |
| AC-15 | The project frame of KAFF-420 sits above the screen. | D-218 item 2 |
| AC-16 | Signing still freezes catalogue values by copy; a copied or custom line holds no foreign key a later price change can reach. | `CLAUDE.md`, Data |

## Not in this story

- Whether the same catalogue item may appear twice in one BOQ: with Karim (D-217 item 22). The build keeps what the app does today.
- The Technical Office review queue screen for custom items.
- Archiving an unsigned project after 30 days: `FollowUpPeriod` already exists; this story only makes the archived BOQ read-only.
