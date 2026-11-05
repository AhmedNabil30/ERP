# KAFF-804 · Warranty runs four months from handover; callbacks attach to it

<!-- kaff id=KAFF-804 slice=8 points=5 state=BUILT -->

**Slice:** 7 (reconciled build order, step 7) · **Epic:** Closure · **Points:** 5 (absorbs `KAFF-805`).
**Owner:** Backend. **No UI criterion is carried here.**
**Spec:** `spec.md` §11 (warranty, callbacks), §13 (`UnderWarranty`).
**Raised by:** the reconciled build order in `stories/project-journey-study-2026-10-08.md`, row 7.

**Status: BLOCKED.** Definition of Ready fails on "Not BLOCKED on an open question" (`Q109`, `Q110`, `Q112`, `Q116`) and on "QA has written at least one scenario that fails". The warranty clock itself is already built; this story's open part is the callback.

## Depends on, and will be refused without

| Needs | Why | State |
|---|---|---|
| Handover, `KAFF-802` | The warranty starts on the handover date | **not built** as a story; **the clock is built** — `Project.HandoverOn` and `Project.WarrantyEndsOn` are set on entering `Handover` [Verified: 2026-11-01 @ `src/Domain/Projects/Project.cs` -> `WarrantyEndsOn`] |
| The `Adjustment` object, `KAFF-512` | A callback cost charged to a subcontractor is a debit note | **not built** |
| Who decides fault, the amount, and the account | Not stated | **unruled** — `Q112` |

## Story

The warranty starts on the handover date and runs four months. A callback, raised during the warranty,
attaches to it. Its cost falls on the subcontractor when the subcontractor was at fault, otherwise on
Kaff.

## Business rules

| # | Rule | Source |
|---|---|---|
| 1 | Warranty starts **automatically** on the handover date and runs **four months** | `spec.md` §11 |
| 2 | The end date is derived from the handover date and stored when the project enters `Handover`; it is never typed | `spec.md` §11 · `Project.WarrantyEndsOn` |
| 3 | Callbacks attach to the warranty | `spec.md` §11 |
| 4 | Callback cost falls on the subcontractor when at fault, otherwise on Kaff | `spec.md` §11 |
| 5 | A Design project has no warranty | `spec.md` §11 · `decisions.md` D-015 |
| 6 | Nothing a callback costs touches the hold; it was released once at handover | `spec.md` §5.1 · `KAFF-802` |
| 7 | Who raises a callback, who decides fault and the amount, whether a callback may be recorded after the four months, and what moves `Handover` to `UnderWarranty` are unruled | `Q109`, `Q110`, `Q112`, `Q116` |
| 8 | Role **and** assignment, server-side | `spec.md` §9 |
| 9 | Every state change writes an audit record | `CLAUDE.md` |

## Acceptance criteria

**AC-804-A — the warranty end date is four months after handover**
Given a project handed over on a date
When its warranty end date is read
Then it is four months after that date, derived, with no field accepting a typed date
*Rules: 1, 2.*

**AC-804-B — a callback attaches to the warranty**
Given a project under warranty
When a callback is raised
Then it is stored attached to that project's warranty with its description and date
*Rule: 3. The role is HELD on `Q109`.*

**AC-804-C — a callback on a project with no warranty**
Given a project in `Active` or a Design project
When a callback is raised
Then it is refused
*Rules: 3, 5.*

**AC-804-D — a callback after the four months — HELD**
Given a project whose warranty has ended
When a callback is raised
Then it follows the ruling
*Rule: none yet. **HELD on `Q116`.***

**AC-804-E — cost allocation — HELD**
Given a fixed callback
When its cost is allocated
Then it falls on the subcontractor if at fault, otherwise on Kaff, as the ruling says who decides and where it books
*Rule: 4. **HELD on `Q112`.***

**AC-804-F — the hold is untouched**
Given a project with a callback costed
When balances are read
Then the hold is exactly zero and no callback posting touched it
*Rule: 6.*

**AC-804-G — role and assignment, direct to the endpoint**
Given a user with the role and no assignment
When they call the callback endpoints
Then they are refused
*Rule: 8.*

**AC-804-H — an audit record**
Given a callback raised and resolved
When the trail is read
Then it carries who, when and what changed
*Rule: 9.*

## Not in this story

Subcontractor retention release at the warranty's end (`KAFF-806`). Closing the project (`KAFF-807`).
Handover (`KAFF-802`). The snag list (`KAFF-800`). Every screen — no Frontend story is cut, so by
`D-117` no UI criterion is written.

## Questions for Karim

1. **`Q116`** — a callback after the four months.
2. **`Q112`** — fault, amount, account.
3. **`Q110`** — what moves `Handover` to `UnderWarranty`.
4. **`Q109`** — who raises a callback.
