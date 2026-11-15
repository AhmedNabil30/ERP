# KAFF-615 · The crew screen under D-217 and D-218: 16 hours, half-hour steps, who edits which day

<!-- kaff id=KAFF-615 slice=6 points=5 state=BUILT -->

**Slice:** 6 — Execution · **Points:** 5.
**Owner:** Backend, then UI Designer (`build` from `.design/v2/team-a/day-labour.html`, commit `ecb140d`).
**Source:** Nabil's rulings D-217 items 1 to 7 and D-218 item 16. Journey brief 2 in
`ux/journey-project-module-2026-11-09.md`.
**Spec:** `spec.md` §8, §9.

## Acceptance criteria

| ID | Criterion | Source |
|---|---|---|
| AC-1 | Hours per man per day are refused above 16 and refused when not a multiple of 0.5. | D-217 items 1, 2 |
| AC-2 | Crew hours may be entered for today or up to three days back, under the check-in rule of KAFF-614 AC-1 and AC-2. | D-217 item 7 |
| AC-3 | The saved crew of today is edited by the engineer who entered it. The supervising site engineer does not edit the crew of today. | D-217 item 3, D-218 item 16 |
| AC-4 | An earlier open day is edited by the supervising site engineer and is read-only for the engineer who entered it. | D-217 item 3 |
| AC-5 | The owner may edit the crew of any open day. | D-218 item 16 |
| AC-6 | "نفس عمالة أمس" copies the men of yesterday and their hours, and is offered on today only. | D-217 item 4, D-218 item 16 |
| AC-7 | The add-worker sheet shows no phone. | D-217 item 6 |
| AC-8 | A back-dated crew day carries the back-dated flag, sent only to the supervising site engineer and the owner. | D-217 item 8 |
| AC-9 | A refused value is not counted in the total of the day. | mock finding, D-218 |
| AC-10 | The screen follows the approved mock, with the project frame of KAFF-420 above it. | D-218 item 2 |
| AC-11 | An edit of saved hours never updates a stored entry in place; it is stored as the daily log stores a correction (KAFF-614 AC-6) and audited. | `CLAUDE.md`, Data and Audit |

## Not in this story

- A link to past days for the supervisor (D-217 item 5).
- Day-labour payment. Money never moves from this screen.
