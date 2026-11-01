# KAFF-316 · Collections — method, date, reference, and the cheque states

<!-- kaff id=KAFF-316 slice=7 points=8 state=NOT-BUILT -->

**Slice:** 7 (reconciled build order, step 7 — *not* the backlog's "Slice 7 — Accounting") · **Epic:** Collections · **Points:** 8.
**Owner:** Backend. **No UI criterion is carried here** (see "Not in this story").
**Spec:** `spec.md` §6.5 (collections and cheques), §6.1 (the posting model), §7 (the chain ends Issued → Collected), §13 (the extract machine ends at `Paid`).
**Raised by:** the reconciled build order in `stories/project-journey-study-2026-10-08.md`, row 7.

**Status: BLOCKED.** Definition of Ready fails on "Not BLOCKED on an open question" (`Q97`, `Q98`, `Q100`, `Q101`, and `Q91` for the cheque postings) and on "QA has written at least one scenario that fails". The criteria that depend on those questions are marked `HELD`. The rest are ready to build once the dependencies below land. Not for a sprint yet.

## Depends on, and will be refused without

| Needs | Why | State |
|---|---|---|
| The extract chain, `KAFF-500` | A collection is money against an **issued** extract | **not built** |
| `PostingSetBuilder` | A collection with withholding (`KAFF-317`) is two or three postings that must commit together | **done** |
| `PostingProjectStatePolicy` | Which project states accept a collection | **done** — `RulingSet.PostingProjectStates`, unruled default |
| The cheque account type and pairs | `Q91` records that no `AccountType` represents a cheque in hand or in transit | **unruled** — `PostingType.ChequeDeposit`, `ChequeClearance`, `ChequeBounce` are in `PostingAccountLegality.PendingQ91` [Verified: 2026-11-01 @ `src/Domain/Treasury/PostingAccountLegality.cs` -> `PendingQ91`] |

**A conflict to settle before this is built, and it is not a business question.** Today
`ExtractPostings.Build` puts the client's **cash** into the extract's own posting set — a
`ClientCollection` posting from `ClientReceivable` to the safe, plus the withheld amount
[Verified: 2026-11-01 @ `src/Domain/Contracts/Billing/ExtractPostings.cs` -> `Build`]. `spec.md` §7 and §13
make collection a **separate step after Issued**. The two cannot both stand. The Architect and `KAFF-500`
must decide whether the cash leg leaves the extract's set when this story is built; `AC-500-A` reproduces
§15's "Client pays" column and will move with it.

## Story

Finance records that a client has paid. The collection says how (cash, cheque or transfer), when, and
under what reference. A cheque has its own life — received, deposited, cleared, or bounced — and each
step is a posting.

## Business rules

| # | Rule | Source |
|---|---|---|
| 1 | Every collection records **method** (cash / cheque / transfer), **date** and **reference**. None is optional | `spec.md` §6.5 |
| 2 | Client collections default to the bank. The default is a default: the method is the recorded fact, not an inference | `spec.md` §6.5 |
| 3 | Cheque states are `received → deposited → cleared → bounced`. A cheque clears or bounces **from deposited**: that is how the posting types are defined | `spec.md` §6.5 · `PostingType.ChequeClearance`, `PostingType.ChequeBounce` |
| 4 | A collection is a posting through `PostingSetBuilder`. A mistaken one is corrected by a **reversing** posting through `ReversesId`. No update path, no delete path | `spec.md` §6.1 · `CLAUDE.md` |
| 5 | Which project states accept a collection is asked of `RulingSet.PostingProjectStates`, never hard-coded in the handler | `PostingProjectStatePolicy` |
| 6 | A collection never nets against the five ledgers | `CLAUDE.md` · `spec.md` §6.4 |
| 7 | Role **and** assignment, server-side. Which permission records a collection, and whether the Owner must approve it, is unruled. Where an approval exists, nobody creates and approves the same movement | `spec.md` §9 · `CLAUDE.md` · `Q97` |
| 8 | Every state change writes an audit record: who, when, what changed | `CLAUDE.md` |
| 9 | Withholding at source on a corporate client's collection is `KAFF-317`. This story records the gross collection only | `spec.md` §6.7 |

## Acceptance criteria

**AC-316-A — a collection records method, date and reference**
Given an issued extract
When Finance records a collection with method, date and reference
Then it is stored with all three, and a request missing any one is refused with a translated message
*Rule: 1.*

**AC-316-B — role and assignment, direct to the endpoint**
Given a user with the right role but no assignment to the project, and a user assigned but with the wrong role
When each calls the collection endpoint directly
Then both are refused, server-side
*Rule: 7. The permission row is HELD on `Q97` — the test is written for whichever row is ruled.*

**AC-316-C — cheque transitions: every legal one, every illegal one**
Given a cheque in each of the four states
When each transition is attempted
Then `received → deposited`, `deposited → cleared` and `deposited → bounced` succeed; every other transition is refused, including `received → cleared` and `cleared → bounced`
*Rule: 3.*

**AC-316-D — an extract becomes `Paid` — HELD**
Given a collection against an issued extract
When the collection settles it
Then the extract moves to `Paid`
*Rule: none yet. **HELD on `Q98`**: whether one payment settles one extract, part of one, or several, and what "settled" means.*

**AC-316-E — a shortfall that withholding does not explain — HELD**
Given a client who pays less or more than the net
When the collection is recorded
Then the system records exactly what was received and writes **no** balancing entry
*Rule: none yet. **HELD on `Q100`.** The "no balancing entry" half is already required by `spec.md` §6.7 and is not held.*

**AC-316-F — a mistaken collection is reversed, never edited**
Given a recorded collection
When Finance corrects it
Then a reversing posting references the original through `ReversesId`, and no posting row is updated or deleted
*Rule: 4.*

**AC-316-G — a bounced cheque — HELD**
Given a cheque that was deposited and then bounces
When the bounce is recorded
Then the bounce posting is written and the extract's state follows the ruling
*Rule: none yet. **HELD on `Q101`** (the extract's state) and `Q91` (the accounts).*

**AC-316-H — the cheque postings — HELD**
Given a cheque deposited, cleared or bounced
When the posting is written
Then it moves between the accounts `Q91` rules
*Rule: none yet. **HELD on `Q91`.** Until ruled the three types stay unenforced and are not guessed.*

**AC-316-I — the project-state policy decides**
Given projects in each of the eight states
When a collection is recorded on each
Then acceptance matches `RulingSet.PostingProjectStates`, and changing the ruling in a test changes the outcome without a handler change
*Rule: 5.*

**AC-316-J — the ledgers do not net**
Given a collection and a client advance both outstanding
When balances are read after the collection
Then the client advance is unchanged by it
*Rule: 6.*

## Not in this story

Withholding at source (`KAFF-317`). Kaff's own withholding on subcontractor payments (`KAFF-318`,
`Q29`). **Every screen:** no Frontend story for collections exists on the board, so by `process/agile.md`
§2a rule 6 and `D-117` no UI criterion is written here — a JSON response is not a rendering. The
collections screen and the cheque register are a companion Frontend story still to be cut. VAT
(`spec.md` §6.7, assumption 15: pending). Bank-guarantee letters. A general ledger.

## Questions for Karim

1. **`Q97`** — does the Owner approve a receipt, or does Finance record it alone?
2. **`Q98`** — one payment per extract, part of one, or several; and what makes an extract `Paid`?
3. **`Q100`** — a shortfall or overpayment withholding does not explain.
4. **`Q101`** — what a bounced cheque does to a `Paid` extract.
5. **`Q91`** (already open) — the accounts the three cheque postings move between, and whether a cheque needs an account type of its own.
