# KAFF-803 · Snag resolution — debit the subcontractor, or absorb it as internal cost

<!-- kaff id=KAFF-803 slice=8 points=5 state=NOT-BUILT -->

**Slice:** 7 (reconciled build order, step 7) · **Epic:** Closure · **Points:** 5.
**Owner:** Backend. **No UI criterion is carried here.**
**Spec:** `spec.md` §11 (snag resolution is a dual toggle), §6.9 (debit note), §5.1 (the hold).
**Raised by:** the reconciled build order in `stories/project-journey-study-2026-10-08.md`, row 7.

**Status: BLOCKED.** Definition of Ready fails on "Not BLOCKED on an open question" (`Q112`, `Q109`), on a dependency not in step 7 (subcontractor sub-ledgers, `KAFF-510`), and on "QA has written at least one scenario that fails".

## Depends on, and will be refused without

| Needs | Why | State |
|---|---|---|
| The snag list, `KAFF-800` | There is nothing to resolve without a snag | **not built** |
| The `Adjustment` object, `KAFF-512` | A debit note is an adjustment | **not built** |
| Subcontractor extracts and sub-ledgers, `KAFF-510` | A debit note lands on a subcontractor's account | **not built**, and **not part of step 7** |
| The cost of a snag, and the account that carries an absorbed one | Neither is stated | **unruled** — `Q112` |

## Story

When a snag is fixed, the cost is either deducted from the subcontractor who owned it, as a debit note,
or absorbed by Kaff as internal cost. The choice is recorded. The hold is never the source of either.

## Business rules

| # | Rule | Source |
|---|---|---|
| 1 | Snag resolution is a **dual toggle**: deduct from the subcontractor (debit note), or absorb as internal cost | `spec.md` §11 |
| 2 | A debit note is an `Adjustment` of the debit-note type, with reason, source document and target account | `spec.md` §6.9 · `KAFF-512` |
| 3 | **A snag never takes anything out of the hold.** The hold only grows until the handover release | `spec.md` §5.1 · `CLAUDE.md` |
| 4 | The choice is recorded with who made it and why | `CLAUDE.md` |
| 5 | A debit note names a subcontractor, so the toggle's first option needs a snag whose owner is one | `spec.md` §6.9, §11 |
| 6 | Who decides the cost, the amount rule, and where an absorbed cost is booked are unruled | `Q112` |
| 7 | Nobody creates and approves the same movement | `CLAUDE.md` · `spec.md` §9 |
| 8 | Role **and** assignment, server-side | `spec.md` §9 · `Q109` |
| 9 | Every state change writes an audit record | `CLAUDE.md` |

## Acceptance criteria

**AC-803-A — the toggle is recorded**
Given a fixed snag
When a user holding the ruled permission resolves it as a debit note, or as absorbed
Then the choice, who made it and the reason are stored
*Rules: 1, 4. The role is HELD on `Q109`.*

**AC-803-B — a debit note needs a subcontractor owner**
Given a snag owned by Kaff
When it is resolved as a debit note
Then it is refused
*Rule: 5.*

**AC-803-C — the hold is untouched**
Given a project with a hold balance
When a snag is resolved either way
Then the hold balance is unchanged
*Rule: 3.*

**AC-803-D — the cost and the absorbed account — HELD**
Given a snag resolved either way
When the cost is booked
Then the amount and the account follow the ruling
*Rule: none yet. **HELD on `Q112`.***

**AC-803-E — a debit note is an adjustment, with reversing postings to correct**
Given a posted debit note
When it is corrected
Then a reversing posting references the original through `ReversesId`
*Rules: 2 · `CLAUDE.md`.*

**AC-803-F — nobody approves their own**
Given the user who raised the resolution
When they attempt to approve it
Then it is refused server-side
*Rule: 7.*

**AC-803-G — role and assignment, direct to the endpoint**
Given a user with the role and no assignment
When they call the endpoint
Then it is refused
*Rule: 8.*

**AC-803-H — an audit record**
Given a resolved snag
When the trail is read
Then it carries who, when, the toggle and the reason
*Rule: 9.*

## Not in this story

Snag creation and status (`KAFF-800`). The `Adjustment` object (`KAFF-512`). Subcontractor extracts
(`KAFF-510`). Callback costs (`KAFF-804`). Every screen — no Frontend story is cut, so by `D-117` no UI
criterion is written.

## Questions for Karim

1. **`Q112`** — who decides the cost, who decides fault, where an absorbed cost goes.
2. **`Q109`** — who may resolve a snag.
