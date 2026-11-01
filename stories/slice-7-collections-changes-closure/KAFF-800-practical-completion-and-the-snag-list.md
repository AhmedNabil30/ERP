# KAFF-800 · Practical completion and the snag list — major snags block handover

<!-- kaff id=KAFF-800 slice=7 points=8 state=NOT-BUILT -->

**Slice:** 7 (reconciled build order, step 7) · **Epic:** Closure · **Points:** 8 (absorbs `KAFF-801`).
**Owner:** Backend. **No UI criterion is carried here.**
**Spec:** `spec.md` §11 (closure, warranty), §13 (project states).
**Raised by:** the reconciled build order in `stories/project-journey-study-2026-10-08.md`, row 7.

**Status: BLOCKED.** Definition of Ready fails on "Not BLOCKED on an open question" (`Q109`, `Q115`) and on "QA has written at least one scenario that fails". This story moves **no money**.

## Depends on, and will be refused without

| Needs | Why | State |
|---|---|---|
| The project state machine | Practical completion is the `Active → HandoverPending` transition | **done** [Verified: 2026-11-01 @ `src/Domain/Projects/Project.cs` -> `ExecutionTransitions`] |
| Permission rows for these acts | None of declare, add, fix, verify exists | **unruled** — `Q109` |
| Subcontractor records | A snag's owner may be a subcontractor | **done** — the master record exists |

## Story

When the work is practically complete, the project moves to `HandoverPending` and a snag list is kept.
Each snag has an owner, a priority and a status. A major snag stops the handover. A minor one does not.

## Business rules

| # | Rule | Source |
|---|---|---|
| 1 | The flow is practical completion → snag list → handover → `UnderWarranty` (4 months) → `Closed` | `spec.md` §11 |
| 2 | A snag carries description, location, owner (Kaff or subcontractor), priority, and status `open → fixed → verified` | `spec.md` §11 |
| 3 | **Major snags block handover. Minor snags do not** | `spec.md` §11 |
| 4 | Practical completion moves an execution project `Active → HandoverPending`. No other source state is legal for it | `ProjectStatus.HandoverPending` doc · `Project` `ExecutionTransitions` |
| 5 | A Design project has no snag list, no handover and no hold | `spec.md` §11 · `decisions.md` D-015 |
| 6 | How "major" relates to a snag's priority, and what unblocks the handover (fixed or verified), is unruled | `Q115` |
| 7 | A snag has no cost in this story. Resolution is `KAFF-803` | `spec.md` §11 |
| 8 | Role **and** assignment, server-side; a control the user may not use is absent | `spec.md` §9 · `Q109` |
| 9 | Every state change writes an audit record: who, when, what, and why where the flow needs it | `CLAUDE.md` |

## Acceptance criteria

**AC-800-A — a snag has all five fields**
Given a snag request missing its location, owner, priority or description
When it is submitted
Then it is refused, and a complete one is stored with status `open`
*Rule: 2.*

**AC-800-B — snag status transitions — partly HELD**
Given snags in each status
When each transition is attempted
Then `open → fixed` and `fixed → verified` succeed and a skip (`open → verified`) is refused
*Rule: 2. Reopening a fixed snag whose fix fails is **HELD on `Q115`**; it is neither allowed nor refused until ruled.*

**AC-800-C — practical completion has one legal source state**
Given projects in each of the eight states
When practical completion is declared on each
Then only `Active` succeeds, and the others are refused by the domain
*Rule: 4.*

**AC-800-D — a Design project has no snag list**
Given a Design project
When a snag is added or practical completion declared
Then both are refused
*Rule: 5.*

**AC-800-E — a major snag blocks handover — HELD in part**
Given a project with an open major snag
When handover is attempted
Then it is refused naming the snag
*Rule: 3. **What counts as resolved is HELD on `Q115`**: the test runs for "open" and is extended once ruled.*

**AC-800-F — a minor snag does not block**
Given a project whose only open snags are minor
When handover is attempted
Then it is not refused on account of them
*Rule: 3.*

**AC-800-G — role and assignment, direct to the endpoint**
Given a user with the role and no assignment
When they call any snag or completion endpoint
Then it is refused, server-side
*Rule: 8. The role itself is HELD on `Q109`.*

**AC-800-H — every change writes an audit record**
Given a snag raised, fixed and verified
When the trail is read
Then each step carries who and when
*Rule: 9.*

## Not in this story

Handover itself and the hold release (`KAFF-802`). Snag cost, debit notes, absorbing cost
(`KAFF-803`). Photos of snags (not in `spec.md` §11). Every screen — no Frontend story is cut, so by
`D-117` no UI criterion is written.

## Questions for Karim

1. **`Q109`** — who may declare practical completion, add and close a snag, hand over, release the hold, start a callback and close.
2. **`Q115`** — major versus priority, what unblocks the handover, and whether a failed fix reopens.
