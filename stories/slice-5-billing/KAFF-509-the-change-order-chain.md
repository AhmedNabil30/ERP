# KAFF-509 · The change order chain — an approved one raises the contract value

<!-- kaff id=KAFF-509 slice=5 points=8 state=NOT-BUILT -->

**Slice:** 7 (reconciled build order, step 7) · **Epic:** Change orders · **Points:** 8.
**Owner:** Backend. **No UI criterion is carried here.**
**Spec:** `spec.md` §13 (change order state machine), §9 (separation of duties), §5.1 (change orders at their own prices), §6.8 (an approved change order raises the budget baseline), §12 (the client approves).
**Raised by:** the reconciled build order in `stories/project-journey-study-2026-10-08.md`, row 7.

**Status: BLOCKED.** Definition of Ready fails on "Not BLOCKED on an open question" (`Q103`, `Q104`, `Q105`) and on "QA has written at least one scenario that fails". The criteria depending on them are `HELD`.

## Depends on, and will be refused without

| Needs | Why | State |
|---|---|---|
| The BOQ, frozen at signature, and `ContractValueSource` | An approved change order **raises** a value that is derived, never typed | **done** — `ContractValueSource`, unruled where noted in its own `RulingNote` |
| `Permission.ChangeOrderApprove` | The Owner's approval step | **done** — Owner, `ProjectScoped`, `TouchesMoney` [Verified: 2026-11-01 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.ChangeOrderApprove`] |
| Permission rows for create, technical review, withdraw | None exists | **unruled** — `Q105` |
| The client's approval | `spec.md` §12 puts it in the portal, and the portal is not built | **unruled** — `Q103` |

## Story

A change order is a priced variation to a Lump Sum contract. It is drafted, reviewed technically,
approved by the client, then approved by the Owner. When approved, the contract value rises. A Cost Plus
or Design change order is only logged: it has no price and moves no money.

## Business rules

| # | Rule | Source |
|---|---|---|
| 1 | **Lump Sum, priced:** `Draft → TechnicalReview → ClientApproval → OwnerApproval → Approved`. Approved **raises the contract value** | `spec.md` §13 |
| 2 | A rejection moves the change order to `Rejected`. `Withdrawn` is the other terminal state | `spec.md` §13 |
| 3 | **Cost Plus and Design:** `Draft → Logged`. Documentary and **unpriced**. No money | `spec.md` §13 |
| 4 | The Owner approves every financial movement. **Finance does not approve change orders** | `spec.md` §9 · `Permission.ChangeOrderApprove` |
| 5 | **Nobody creates and approves the same movement.** Each gate actor is stored on the record | `CLAUDE.md` · `spec.md` §9 |
| 6 | A rejection records a **stored reason**, and the audit record carries who, when and why. Never a silent step-back | `CLAUDE.md` · `spec.md` §7 |
| 7 | The contract value stays **derived**: BOQ total plus approved change orders. It is never a typed field | `stories/project-journey-study-2026-10-08.md`, build order step 2 · `ContractValueSource` |
| 8 | An approved change order raises the budget baseline. That is `KAFF-709`; this story only exposes the approved total | `spec.md` §6.8 |
| 9 | A change order is never merged into an original BOQ line. A signed BOQ line is untouched | `spec.md` §4.4, §5.1 |
| 10 | The client approves priced change orders, in the portal. How that approval is recorded before the portal exists is unruled | `spec.md` §12 · `Q103` |
| 11 | Role **and** assignment on every step, server-side. A step the user may not take is absent, never disabled | `spec.md` §9 · the journey study, "Design principles" |
| 12 | Every state change writes an audit record | `CLAUDE.md` |

## Acceptance criteria

**AC-509-A — every legal transition, every illegal one**
Given a priced change order in each state
When each transition is attempted
Then exactly those of rule 1, `Rejected` and `Withdrawn` succeed; all others are refused, including skipping `ClientApproval` or `OwnerApproval`
*Rules: 1, 2.*

**AC-509-B — how a line is priced — HELD**
Given a change-order line
When it is priced
Then the price follows the ruling
*Rule: none yet. **HELD on `Q104`.***

**AC-509-C — an approved change order raises the contract value**
Given a Lump Sum contract worth 1,000,000 and a change order of 100,000
When the Owner approves it
Then the contract value reads 1,100,000, derived, and the BOQ lines are unchanged
*Rules: 1, 7, 9.*

**AC-509-D — the client's approval — HELD**
Given a change order at `ClientApproval`
When the client's approval is recorded
Then it is recorded by the means the ruling names
*Rule: none yet. **HELD on `Q103`.***

**AC-509-E — only the Owner approves; Finance cannot**
Given a Finance user and a Technical Office user assigned to the project
When each attempts the Owner approval directly
Then both are refused, server-side
*Rule: 4.*

**AC-509-F — nobody approves their own**
Given the user who created a change order
When they attempt any later gate on it
Then it is refused server-side, and the control was **absent** on the screen the data feeds
*Rule: 5.*

**AC-509-G — a rejection returns with a stored reason**
Given a change order at each gate
When it is rejected without a reason, and then with one
Then the first is refused; the second stores the reason and writes an audit record with who, when and why
*Rule: 6.*

**AC-509-H — Cost Plus and Design only log**
Given a Cost Plus project and a Design project
When a change order is created
Then it ends at `Logged`, carries no price, and no money posting exists for it
*Rule: 3.*

**AC-509-I — role and assignment, direct to the endpoint**
Given a user with the role but no assignment
When they call any step directly
Then it is refused
*Rule: 11. The non-approval rows are HELD on `Q105`.*

**AC-509-J — a change order cannot reach a signed BOQ line**
Given a signed BOQ and an approved change order
When the BOQ lines are read
Then they are exactly what was signed
*Rule: 9.*

## Not in this story

Billing change orders on an extract (`KAFF-508`). The client approving through the portal
(`KAFF-812`). The budget baseline (`KAFF-709`). The billable design revision beyond the agreed rounds
(`spec.md` §5.3 gives no price). The change order screens: no Frontend story exists, so by `D-117` no UI
criterion is written.

## Questions for Karim

1. **`Q103`** — who records the client's approval before the portal exists.
2. **`Q104`** — how a change-order line is priced.
3. **`Q105`** — who creates, reviews technically and withdraws.
