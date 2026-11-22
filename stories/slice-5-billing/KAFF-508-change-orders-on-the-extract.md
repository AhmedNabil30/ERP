# KAFF-508 · Change orders appear on the extract in their own section, at their own prices

<!-- kaff id=KAFF-508 slice=5 points=8 state=ACCEPTED verdict=CONDITIONAL at=c7460aa on=2026-11-22 -->

**Slice:** 7 (reconciled build order, step 7) · **Epic:** Change orders · **Points:** 8.
**Owner:** Backend. **No UI criterion is carried here.**
**Spec:** `spec.md` §5.1 ("Change orders MUST appear in their own section at their own prices, never merged into original BOQ lines"), §7, §15.
**Raised by:** the reconciled build order in `stories/project-journey-study-2026-10-08.md`, row 7.

**Status: BLOCKED.** Definition of Ready fails on "Not BLOCKED on an open question" (`Q102`, and `Q104` through `KAFF-509`) and on "QA has written at least one scenario that fails".

## Depends on, and will be refused without

| Needs | Why | State |
|---|---|---|
| The extract chain, `KAFF-500` | The change-order section is part of the extract | **not built** |
| Approved change orders, `KAFF-509` | Only an **approved** one can be billed | **not built** |
| The Lump Sum calculator | It refuses a change order today | **done** — it returns `BillingErrors.ChangeOrderTreatmentUnruled` [Verified: 2026-11-01 @ `src/Domain/Contracts/Billing/Calculators.cs` -> `ChangeOrderTreatmentUnruled`]; this story replaces that refusal once `Q102` is ruled |

## Story

An approved change order is billed on the extract, in a section of its own, at the price it was approved
at. It is never folded into an original BOQ line.

## Business rules

| # | Rule | Source |
|---|---|---|
| 1 | Change orders appear in **their own section** of the extract, at **their own prices**, never merged into original BOQ lines | `spec.md` §5.1 |
| 2 | Only an **approved** change order can be billed | `spec.md` §13 |
| 3 | Billing a change order raises the work value of the extract. **Whether the hold and the advance recovery apply to it is unruled**; the calculator keeps refusing until it is | `spec.md` §5.1 · `decisions.md` D-186 · `Q102` |
| 4 | The hold only grows and nothing but the handover release takes from it, change orders included | `spec.md` §5.1 · `CLAUDE.md` |
| 5 | Value is billed as the period's difference: cumulative certified less previously certified, so nothing is billed twice. The same holds for change-order lines | `spec.md` §5.1 ("period value") |
| 6 | Cost Plus and Design change orders are `Logged` and unpriced. They never appear in the priced section | `spec.md` §13 |
| 7 | The visible column foots, and each figure declares its kind | `Q82` / `Q21` · the journey study, "Design principles" |
| 8 | Every other extract rule — gates, Owner approval, rejection to Draft — is `KAFF-500`'s and is not restated | `KAFF-500` |

## Acceptance criteria

**AC-508-A — a change order is its own section**
Given an extract on a contract with an approved change order
When the extract's document is read
Then the change order is in its own section at its approved price, and no original BOQ line carries it
*Rule: 1.*

**AC-508-B — an unapproved change order is never billed**
Given a change order in any state before `Approved`
When an extract is prepared
Then it is not offered and cannot be added
*Rule: 2.*

**AC-508-C — hold and advance recovery on a change order — HELD**
Given an extract billing 100,000 of change-order value
When the deductions are computed
Then the hold and the advance recovery follow the ruling
*Rule: 3. **HELD on `Q102`.** Until then the calculator refuses with `ChangeOrderTreatmentUnruled`, which stays correct.*

**AC-508-D — billed once**
Given a change order already billed on one extract
When it is offered on the next
Then only the unbilled remainder is offered
*Rule: 5.*

**AC-508-E — Cost Plus and Design stay out**
Given a `Logged` change order on a Cost Plus project
When an extract is prepared
Then no priced change-order section appears
*Rule: 6.*

**AC-508-F — the column foots**
Given an extract with a change-order section
When the printed figures are summed
Then they equal the printed total exactly
*Rule: 7.*

**AC-508-G — §15 is unchanged by a contract without change orders**
Given the §15 acceptance project, which has none
When both extracts run
Then every §15 figure is reproduced
*Rule: 8. Guards against this story moving the acceptance criterion.*

## Not in this story

The change-order chain and the value rise (`KAFF-509`). The budget baseline (`KAFF-709`). The extract's
other behaviour (`KAFF-500`). Every screen — no Frontend story is cut, so by `D-117` no UI criterion is
written.

## Questions for Karim

1. **`Q102`** — whether the hold and the advance recovery apply to change-order value.
2. **`Q104`** (via `KAFF-509`) — how a change-order line is priced.
