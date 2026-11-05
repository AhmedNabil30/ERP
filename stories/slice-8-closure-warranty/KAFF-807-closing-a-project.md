# KAFF-807 · A project closes only when every account is settled

<!-- kaff id=KAFF-807 slice=8 points=8 state=VERIFIED verdict=CONDITIONAL at=86b9f13 on=2026-11-06 -->

**Slice:** 7 (reconciled build order, step 7) · **Epic:** Closure · **Points:** 8.
**Owner:** Backend. **No UI criterion is carried here.**
**Spec:** `spec.md` §11 ("A project closes only when all accounts are settled"), §13, §15 (the invariants).
**Raised by:** the reconciled build order in `stories/project-journey-study-2026-10-08.md`, row 7.

**Status: BLOCKED.** Definition of Ready fails on "Not BLOCKED on an open question" (`Q109`, `Q113`, `Q116`) and on "QA has written at least one scenario that fails". Closure moves no money itself; it **reads** the ledgers.

## Depends on, and will be refused without

| Needs | Why | State |
|---|---|---|
| The extract chain, `KAFF-500` | "Extracts issued" has to mean something | **not built** |
| Collections, `KAFF-316` | Whether the client has paid is a closure question | **not built** |
| Handover and the hold release, `KAFF-802` | "Hold released" is a condition | **not built** |
| Warranty, `KAFF-804` | The state before `Closed` is `UnderWarranty` | **not built** |
| Subcontractor sub-ledgers, `KAFF-510` | "Subcontractor accounts closed" is a condition | **not built**, **not part of step 7** |
| The balance read model | Every condition is derived from postings | **done** — `BalanceQueries` [Verified: 2026-11-01 @ `src/Domain/Treasury/BalanceQueries.cs`] |

## Story

A project in `UnderWarranty` moves to `Closed` only when all of its accounts are settled. The check is
a reading of the ledgers, not a checkbox.

## Business rules

| # | Rule | Source |
|---|---|---|
| 1 | A project closes only when **all accounts are settled**: extracts issued, advance recovered, hold released, subcontractor accounts closed | `spec.md` §11 |
| 2 | Each condition is **derived from postings**: the client advance balance is exactly zero, the hold balance is exactly zero, and so on. No condition is a stored flag or a typed tick | `CLAUDE.md` · `spec.md` §6.1, §15 |
| 3 | A refusal names every unmet condition, not only the first | `spec.md` §11 |
| 4 | Closing writes `ClosedOn` and moves `UnderWarranty → Closed`. No other source state is legal for an execution project | `spec.md` §13 · `Project` `ExecutionTransitions` |
| 5 | A `Terminated` project does not reach `Closed` through this story. Its end state is unruled | `spec.md` §13 · `Q108` |
| 6 | A closed project accepts collections and nothing else, in the default table | `PostingProjectStatePolicy` |
| 7 | A Design project closes differently: final documents delivered, last 10% collected, IP transfers. It is `KAFF-808`, not this story | `spec.md` §11 |
| 8 | The five ledgers never net against each other when "settled" is judged: each is read separately | `CLAUDE.md` · `spec.md` §6.4 |
| 9 | Whether the client's receivable must be zero, whether open تشوينات, عهدة or firm advance block closure, and what "closed" means for a subcontractor account are unruled | `Q113` |
| 10 | Whether the four months must have passed is unruled | `Q116` |
| 11 | Role **and** assignment, server-side | `spec.md` §9 · `Q109` |
| 12 | Every state change writes an audit record | `CLAUDE.md` |

## Acceptance criteria

**AC-807-A — a fully settled project closes**
Given the §15 acceptance project after its extracts and the hold release, with the advance at zero
When closure is requested
Then it succeeds, `ClosedOn` is stamped, and the project is `Closed`
*Rules: 1, 2, 4.*

**AC-807-B — each named condition blocks on its own**
Given a project failing exactly one of: an extract not issued, an advance balance above zero, a hold balance above zero
When closure is requested
Then it is refused naming that condition, and a project failing two names both
*Rules: 1, 3.*

**AC-807-C — the conditions `spec.md` does not name — HELD**
Given a project with an unpaid receivable, open تشوينات, an open عهدة or an open firm advance
When closure is requested
Then it follows the ruling
*Rule: 9. **HELD on `Q113`.***

**AC-807-D — the conditions are derived, never flagged**
Given a project whose hold has a posting history
When the closure check runs
Then it reads the balance from postings, and no request field can mark a condition as met
*Rule: 2.*

**AC-807-E — only `UnderWarranty` closes**
Given execution projects in each of the eight states
When closure is requested on each
Then only `UnderWarranty` can succeed
*Rule: 4.*

**AC-807-F — a terminated project is not closed here**
Given a `Terminated` project
When closure is requested
Then it is refused
*Rule: 5.*

**AC-807-G — after closing, only collections post**
Given a `Closed` project
When an extract, a hold release and a collection are attempted
Then only the collection is accepted, per `RulingSet.PostingProjectStates`
*Rule: 6.*

**AC-807-H — the four months — HELD**
Given a project under warranty before its end date, with all accounts settled
When closure is requested
Then it follows the ruling
*Rule: 10. **HELD on `Q116`.***

**AC-807-I — a Design project is not closed by this story**
Given a Design project
When this closure is requested
Then it is refused as not applicable
*Rule: 7.*

**AC-807-J — role and assignment, direct to the endpoint**
Given a user with the role and no assignment
When they call the endpoint
Then it is refused, server-side
*Rule: 11. The role is HELD on `Q109`.*

**AC-807-K — the ledgers are judged separately**
Given a client advance still outstanding by 10,000 and a firm advance recovered 10,000 past zero
When closure is requested
Then the client advance is still reported as unmet, not offset by the firm advance
*Rule: 8.*

**AC-807-L — closure writes an audit record**
Given a closed project
When the trail is read
Then it carries who, when and the readings of each condition at that moment
*Rule: 12.*

## Not in this story

Design closure (`KAFF-808`). Furnishing as a linked child project (`KAFF-809`). Marketing assets and
the referral opportunity (`KAFF-814`). Termination and any settlement (`Q108`, `KAFF-512`). Every
screen — no Frontend story is cut, so by `D-117` no UI criterion is written.

## Questions for Karim

1. **`Q113`** — the conditions beyond the four named, and what "closed" means for a subcontractor account.
2. **`Q116`** — whether the warranty must run out first.
3. **`Q109`** — who closes.
4. **`Q108`** — how a terminated project ends.
