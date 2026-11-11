# KAFF-614 · The daily log under D-217 and D-218: check-in first, three days back, edit a quantity, stoppage as text

<!-- kaff id=KAFF-614 slice=6 points=8 state=COMMITTED -->

**Slice:** 6 — Execution · **Points:** 8.
**Owner:** Backend, then UI Designer (`build` from `.design/v2/team-a/daily-log.html`, commit `b0066c6`).
**Source:** Nabil's rulings D-217 items 7 to 18 and D-218 items 14 and 15. Journey brief 1 in
`ux/journey-project-module-2026-11-09.md`.
**Spec:** `spec.md` §8.

## Acceptance criteria

| ID | Criterion | Source |
|---|---|---|
| AC-1 | No daily-log entry of any kind is saved for a user who has not checked in today on that project. The refusal names the check-in. | D-217 item 9 |
| AC-2 | An entry may be dated today or up to three days back, and no further. A past day gets no check-in of its own; the engineer must be checked in today. | D-217 items 7, 10 |
| AC-3 | An entry dated before today carries a back-dated flag. The API sends the flag only to the supervising site engineer and the owner. | D-217 item 8 |
| AC-4 | A second quantity for the same item on the same day adds to the first. | D-217 item 11 |
| AC-5 | A saved quantity can be corrected: today by the engineer who entered it, an earlier open day by the supervising site engineer. Anyone else is refused. | D-218 item 14 |
| AC-6 | A correction is stored as a new signed delta entry that names the entry it corrects. No entry is updated or deleted. The log reads back one corrected figure per saved quantity. | D-218, structural decision |
| AC-7 | A second note on the same day is kept beside the first. | D-217 item 12 |
| AC-8 | A stoppage reason is free text. It may be recorded on a running project without changing the project status, on today or any open past day. A second reason the same day is kept beside the first. | D-217 items 14, 16; D-218 item 15 |
| AC-9 | A stopped project still records quantities, materials and photos. | D-217 item 15 |
| AC-10 | The weather entry kind is gone from the API and the screen. Stored weather entries are left as they are and are not deleted. | D-217 item 16, D-215 |
| AC-11 | The screen follows the approved mock: before check-in it shows only the check-in; after it, the day switch (today and three days back), the entry kinds, and "تعديل" on each saved quantity. The project frame of KAFF-420 sits above it. | D-217 item 9, D-218 item 2 |
| AC-12 | At 360 and 390 px the quantity field is fully visible above the bottom bar while typing. | mock finding, D-218 |
| AC-13 | Every state change writes an audit record, including a correction. | `CLAUDE.md`, Audit |

## Not in this story

- Whether the owner may correct a quantity: not ruled (D-218).
- The confirmed quantity of an extract (D-217 item 13): comes with the extract screens.
- The list of material units: stays as built until Karim confirms (D-217 item 17).
