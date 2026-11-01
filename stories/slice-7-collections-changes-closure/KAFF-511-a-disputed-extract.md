# KAFF-511 · A disputed issued extract resolves as a revised extract or a credit note

<!-- kaff id=KAFF-511 slice=7 points=8 state=NOT-BUILT -->

**Slice:** 7 (reconciled build order, step 7) · **Epic:** Extract · **Points:** 8.
**Owner:** Backend. **No UI criterion is carried here.**
**Spec:** `spec.md` §7 ("A disputed issued extract resolves as either a revised extract or a credit note"), §13 (`Disputed`, `Void`), §6.9, §6.1.
**Raised by:** the reconciled build order in `stories/project-journey-study-2026-10-08.md`, row 7; named out of scope in `KAFF-500`.

**Status: BLOCKED.** Definition of Ready fails on "Not BLOCKED on an open question" (`Q106`, and `Q107` through `KAFF-512`) and on "QA has written at least one scenario that fails". The resolution criteria are `HELD`; the dispute state is not.

## Depends on, and will be refused without

| Needs | Why | State |
|---|---|---|
| The extract chain, `KAFF-500` | There is nothing to dispute until an extract is issued | **not built** |
| The `Adjustment` object, `KAFF-512` | A credit note is an adjustment | **not built** |
| Collections, `KAFF-316` | Whether a paid extract can be disputed touches `Paid` | **not built** |

## Story

A client disputes an issued extract. The extract enters `Disputed`. It then ends one of two ways: a
revised extract replaces it (the original becomes `Void`), or a credit note offsets it. Either way the
original's postings are never edited.

## Business rules

| # | Rule | Source |
|---|---|---|
| 1 | An issued extract may become `Disputed`. States `Disputed` and `Void` exist in the extract machine | `spec.md` §13 |
| 2 | A disputed issued extract resolves as **either** a revised extract **or** a credit note | `spec.md` §7 |
| 3 | A credit note is an `Adjustment`, so it is reversing postings through `ReversesId`. Never an edit, never a delete | `spec.md` §6.9, §6.1 · `KAFF-512` rule 4 |
| 4 | If a resolution reverses a posted extract, it does so with reversing postings, never by deleting. Whether a revised extract sets the original to `Void` is unruled | `CLAUDE.md` · `spec.md` §6.1 · `Q106` |
| 5 | **A dispute never touches the hold.** Nothing comes out of it mid-project, however the dispute resolves | `spec.md` §5.1 · `CLAUDE.md` |
| 6 | A dispute stores a **reason** and its resolution stores a **reason**. The audit record carries who, when and why | `CLAUDE.md` · `spec.md` §7 |
| 7 | Nobody creates and approves the same movement, on the credit note and on the revised extract | `CLAUDE.md` · `spec.md` §9 |
| 8 | Who raises a dispute, who chooses the resolution, whether a `Paid` extract can be disputed, and whether a revised extract re-runs the whole chain are unruled | `Q106` |
| 9 | Role **and** assignment, server-side | `spec.md` §9 |

## Acceptance criteria

**AC-511-A — an issued extract can be disputed, and a draft cannot**
Given extracts in each state
When a dispute is raised on each
Then only an `Issued` one succeeds, with a stored reason
*Rules: 1, 6. Whether `Paid` is allowed is `AC-511-D`'s question.*

**AC-511-B — the original's postings are never edited**
Given a disputed extract that had posted
When it is resolved either way
Then no posting row is updated or deleted, and any correction is a reversing posting referencing the original
*Rules: 3, 4.*

**AC-511-C — resolving as a credit note — HELD**
Given a disputed extract
When it is resolved as a credit note
Then the credit note follows the ruling for who decides, what it reverses, and the approval it needs
*Rules: 2, 3. **HELD on `Q106` and `Q107`.***

**AC-511-D — resolving as a revised extract — HELD**
Given a disputed extract
When it is resolved as a revised extract
Then the original's fate (`Void` or otherwise), how much of the chain the revision re-runs, and the treatment of a `Paid` original all follow the ruling
*Rules: 2, 4. **HELD on `Q106`.***

**AC-511-E — the hold is untouched**
Given an extract disputed on a project with a hold balance
When it resolves by either route
Then the hold balance is unchanged until the handover release
*Rule: 5.*

**AC-511-F — nobody approves their own**
Given the user who raised or resolved a dispute
When they attempt to approve its credit note or revision
Then it is refused server-side
*Rule: 7.*

**AC-511-G — role and assignment, direct to the endpoint**
Given a user with the role and no assignment
When they call the dispute endpoints
Then they are refused
*Rule: 9. The role itself is HELD on `Q106`.*

**AC-511-H — a dispute writes an audit record**
Given a dispute raised and resolved
When the trail is read
Then it carries who, when, what changed and both reasons
*Rule: 6.*

## Not in this story

The `Adjustment` object (`KAFF-512`). Collections (`KAFF-316`). The extract's own chain (`KAFF-500`).
A subcontractor extract dispute (`KAFF-510`, not in step 7). Every screen — no Frontend story is cut, so
by `D-117` no UI criterion is written.

## Questions for Karim

1. **`Q106`** — who raises, who decides, whether a paid extract can be disputed, whether a revision re-runs the chain.
2. **`Q107`** (via `KAFF-512`) — the accounts a credit note moves between, and Owner approval.
