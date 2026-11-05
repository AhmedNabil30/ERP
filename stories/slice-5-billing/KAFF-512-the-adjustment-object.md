# KAFF-512 · One `Adjustment` object for every case where money flows back

<!-- kaff id=KAFF-512 slice=5 points=8 state=BUILT -->

**Slice:** 7 (reconciled build order, step 7) · **Epic:** Adjustments · **Points:** 8.
**Owner:** Backend. **No UI criterion is carried here.**
**Spec:** `spec.md` §6.9 (adjustments), §6.1 (the posting model), §5.4 (the 30% design credit), §11 (debit note), §13 (`Terminated`).
**Raised by:** the reconciled build order in `stories/project-journey-study-2026-10-08.md`, row 7, and decision 8 there.

**Status: BLOCKED.** Definition of Ready fails on "Not BLOCKED on an open question" (`Q107`, and `Q108` which blocks one whole type) and on "QA has written at least one scenario that fails". **The termination-settlement type is `BLOCKED` outright: this story specifies no settlement.**

## Depends on, and will be refused without

| Needs | Why | State |
|---|---|---|
| `PostingSetBuilder` and the reversing-posting path | An adjustment posts like any movement; a credit note is reversing postings | **done** |
| `PostingType.CreditNote`, `DebitNote`, `Adjustment` | The types exist | **done** [Verified: 2026-11-01 @ `src/Domain/Treasury/PostingType.cs` -> `CreditNote`, `DebitNote`, `Adjustment`] |
| Account pairs for these types | `PostingAccountLegality` deliberately does not enforce `Adjustment` and has no pair for the other two | **unruled** — `Q107` |
| Subcontractor sub-ledgers, `KAFF-510` | A debit note lands on one | **not built**, and not part of step 7 |

## Story

Whenever money flows back — a credit note to a client, a debit note to a subcontractor, the 30% design
credit, a termination settlement — it is one `Adjustment`: type, reason, source document, target
account. It posts like any other movement.

## Business rules

| # | Rule | Source |
|---|---|---|
| 1 | One `Adjustment` object. Fields: **type, reason, source document, target account** | `spec.md` §6.9 |
| 2 | It posts like any other movement, through `PostingSetBuilder` | `spec.md` §6.9 |
| 3 | Types it covers: client credit note, subcontractor debit note, the 30% design credit, termination settlement | `spec.md` §6.9 |
| 4 | **A credit note is reversing postings, not an edit.** The original postings are never updated or deleted; each is reversed through `ReversesId` | `spec.md` §6.1 · `CLAUDE.md` |
| 5 | **A reason is required and stored.** No adjustment without one | `spec.md` §6.9 · `CLAUDE.md` |
| 6 | **The hold only grows.** No adjustment, credit note or debit note touches the hold ledger before handover | `spec.md` §5.1 ("not a snag, not a debit note, not an adjustment") · `CLAUDE.md` |
| 7 | The five ledgers never net against each other, adjustments included | `CLAUDE.md` · `spec.md` §6.4 |
| 8 | Project-state legality comes from `RulingSet.PostingProjectStates`: `CreditNote`, `DebitNote` and `Adjustment` are accepted in a `Terminated` project in the default table | `PostingProjectStatePolicy` |
| 9 | Nobody creates and approves the same movement. Whether the Owner approves every adjustment is `Q107`; the default reading of `spec.md` §9 is yes | `CLAUDE.md` · `spec.md` §9 · `Q107` |
| 10 | Role **and** assignment, server-side | `spec.md` §9 |
| 11 | Every adjustment writes an audit record: who, when, what, why | `CLAUDE.md` |
| 12 | The 30% design credit is posted when a design project is linked to an execution project at execution signature. This story provides the object; the link is a separate story | `spec.md` §5.4 |
| 13 | **Termination settlement: nothing in `spec.md` says what settles.** No rule is written. See `Q108` | `spec.md` §13, §6.9 · `Q108` |

## Acceptance criteria

**AC-512-A — an adjustment has all four fields**
Given a request missing its reason, its source document, its type or its target account
When it is submitted
Then it is refused, and nothing is posted
*Rules: 1, 5.*

**AC-512-B — the accounts a credit note and a debit note move between — HELD**
Given a credit note to a client and a debit note to a subcontractor
When each posts
Then it moves between the accounts the ruling names
*Rule: none yet. **HELD on `Q107`.***

**AC-512-C — a credit note reverses, it does not edit**
Given an issued extract with its postings
When a credit note is raised against it
Then reversing postings reference the originals through `ReversesId`, and no posting row is updated or deleted
*Rule: 4.*

**AC-512-D — nothing debits the hold before handover**
Given a project in `Active`
When any adjustment, credit note or debit note names the hold account
Then it is refused, in the domain
*Rule: 6.*

**AC-512-E — the ledgers do not net**
Given a client advance, a hold and a firm advance outstanding
When a credit note posts
Then no ledger's balance is offset against another
*Rule: 7.*

**AC-512-F — the project-state table decides**
Given projects in each of the eight states
When an adjustment is attempted on each
Then acceptance matches `RulingSet.PostingProjectStates`
*Rule: 8.*

**AC-512-G — nobody approves their own**
Given the user who raised an adjustment
When they attempt to approve it
Then it is refused server-side
*Rule: 9.*

**AC-512-H — role and assignment, direct to the endpoint**
Given a user with the role and no assignment, and a user assigned with the wrong role
When each calls the endpoint directly
Then both are refused
*Rule: 10. Which role holds the row is not stated by `spec.md`; the test is written for whichever row is ruled (`Q107` for approval).*

**AC-512-I — an adjustment writes an audit record**
Given a posted adjustment
When the audit trail is read
Then it carries who, when, the type and the reason
*Rule: 11.*

**AC-512-J — termination settlement — BLOCKED**
Given a `Terminated` project
When a settlement is requested
Then **no behaviour is specified.**
*Rule: 13. **BLOCKED on `Q108`.** No criterion can be written without inventing the settlement, and none is.*

## Not in this story

A termination settlement of any kind (`Q108`). The 30% design credit's trigger (linked projects,
`spec.md` §5.4). Disputed extracts (`KAFF-511`). Snag debit notes (`KAFF-803`). Every screen — no
Frontend story is cut, so by `D-117` no UI criterion is written.

## Questions for Karim

1. **`Q107`** — the account pairs for a credit note and a debit note, and Owner approval.
2. **`Q108`** — what a termination settles. Hold, client advance, تشوينات, unpaid extracts, subcontractor balances, and who approves.
