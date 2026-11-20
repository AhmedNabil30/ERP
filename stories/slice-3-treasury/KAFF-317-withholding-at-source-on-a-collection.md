# KAFF-317 · Withholding at source on a corporate client's collection

<!-- kaff id=KAFF-317 slice=3 points=8 state=VERIFIED verdict=CONDITIONAL at=2be5a96 on=2026-11-20 -->

**Slice:** 7 (reconciled build order, step 7) · **Epic:** Collections · **Points:** 8.
**Owner:** Backend. **No UI criterion is carried here.**
**Spec:** `spec.md` §6.7 (withholding tax — and this is not a tax module), §15 (the worked example carries no withholding).
**Raised by:** the reconciled build order in `stories/project-journey-study-2026-10-08.md`, row 7.

**Status: BLOCKED.** Definition of Ready fails on "Not BLOCKED on an open question" (`Q99`, `Q30`, and `Q114` for the unruled `RulingSet.WithholdingBase`) and on "QA has written at least one scenario that fails". The criteria depending on them are `HELD`.

## Depends on, and will be refused without

| Needs | Why | State |
|---|---|---|
| Collections, `KAFF-316` | Withholding is recorded **on the collection** | **not built** |
| The contract's withholding category | The rate belongs to the contract, set by Finance | **done** — `Project.WithholdingCategory`, `Project.SetWithholding` [Verified: 2026-11-01 @ `src/Domain/Projects/Project.cs` -> `SetWithholding`]; the endpoint that sets it is `KAFF-416` |
| `RulingSet.WithholdingBase` | Which figure the rate is a percentage of | `WithholdingBaseRule`, default `NetPayable`, hold release carries withholding, **unruled** |
| The asset account | Withheld tax is a recoverable asset | **done** — `AccountType.TaxWithheldAtSource`, `PostingType.TaxWithheldAtSource` [Verified: 2026-11-01 @ `src/Domain/Treasury/AccountType.cs` -> `TaxWithheldAtSource`] |

**Check on build, do not trust:** `decisions.md` D-186 item 5 says the account pair for the withheld
posting is "still pending Q91". `PostingAccountLegality.PendingQ91` does not list `TaxWithheldAtSource`
[Verified: 2026-11-01 @ `src/Domain/Treasury/PostingAccountLegality.cs` -> `PendingQ91`]. The builder
must read the file, not D-186.

## Story

When a corporate client pays, it withholds a percentage and transfers the rest. The collection records
the amount withheld, so cash reconciles against the extract and nobody invents an adjustment to close
the gap.

## Business rules

| # | Rule | Source |
|---|---|---|
| 1 | Egyptian withholding at source: 1% contracting and supplies, 3% services, 5% professional fees, on the amount **before VAT** | `spec.md` §6.7 |
| 2 | When a corporate client pays, the collection MUST record the withheld amount, posted to a "tax withheld at source" **asset**, so cash reconciles | `spec.md` §6.7 |
| 3 | **The rate belongs to the contract, not the client.** Finance sets it, Marketing cannot | `spec.md` §6.7 amendment · `decisions.md` D-049 rulings 9, 10 |
| 4 | An individual client does not withhold. A rate on an individual's contract is refused, and a collection from one records no withholding | `spec.md` §6.7 · D-049 |
| 5 | The amount the rate applies to comes from `RulingSet.WithholdingBase` and nowhere else. Releasing the hold follows the same ruling's `HoldReleaseCarriesWithholding` | `WithholdingBaseRule` |
| 6 | **Two fields and two accounts. That is the whole of it.** No tax module, no ETA e-invoicing | `spec.md` §6.7 · `CLAUDE.md` |
| 7 | Cash received equals the net payable less the amount withheld | `spec.md` §6.7 |
| 8 | The collection and its withholding posting commit together or not at all | `PostingSetBuilder` |
| 9 | VAT is absent. No output VAT line, no VAT payable account, until Kaff's registration is confirmed | `spec.md` §6.7 · §16 assumption 15 |
| 10 | Role, assignment and approval are those of `KAFF-316` rule 7 | `spec.md` §9 · `Q97` |
| 11 | Every state change writes an audit record | `CLAUDE.md` |

## Acceptance criteria

**AC-317-A — a corporate client withholds and cash reconciles**
Given a corporate client whose contract carries a 1% category and an issued extract
When the collection records the withheld amount
Then a `TaxWithheldAtSource` posting is written to the asset account, the cash posting equals net payable less withheld, and the two together equal the net payable
*Rules: 1, 2, 7, 8.*

**AC-317-B — an individual client withholds nothing**
Given a contract whose client is an individual
When a collection is recorded
Then no withholding posting is written, and an attempt to record a withheld amount is refused
*Rule: 4.*

**AC-317-C — the client withheld a different amount than the rate says — HELD**
Given a collection where the client's withheld amount differs from the rate's figure
When it is recorded
Then the recorded figure and the handling of the difference follow the ruling
*Rule: none yet. **HELD on `Q99`.***

**AC-317-D — the base follows the ruling**
Given the same extract under each of the three bases in `WithholdingBaseKind`
When the expected withholding is computed
Then each equals the rate times that base, and flipping `RulingSet.WithholdingBase` in a test moves the figure with no handler change
*Rule: 5.*

**AC-317-E — the hold release withholds only if the ruling says so**
Given the hold released at handover
When the collection of the release is recorded under `HoldReleaseCarriesWithholding` true, then false
Then withholding is written in the first case and not in the second
*Rule: 5. The default itself is unruled; the test flips it.*

**AC-317-F — a contract with no category withholds nothing**
Given a contract whose withholding category is `None`
When a collection is recorded
Then no withholding posting is written
*Rules: 3, 4.*

**AC-317-G — the rate changes after the first issued extract — HELD**
Given a contract with an issued extract
When its withholding category is changed
Then the change is allowed or refused according to the ruling, and two extracts on one contract stay reconcilable
*Rule: none yet. **HELD on `Q30`.***

**AC-317-H — only two accounts, only two fields**
Given the withholding code paths
When the account types and request fields they touch are listed
Then the only tax accounts are `TaxWithheldAtSource` and the liability `TaxWithholdingPayable`, and the only withholding fields are the contract's category and the collection's withheld amount
*Rule: 6.*

**AC-317-I — the commit is atomic**
Given a collection whose withholding posting would breach a guard
When it is submitted
Then neither posting is written and the refusal names the reason
*Rule: 8.*

**AC-317-J — role and assignment**
Given a user without the role, and a user with it but no assignment
When each calls the endpoint directly
Then both are refused, server-side
*Rule: 10.*

## Not in this story

Kaff's own withholding on subcontractor and supplier payments (`KAFF-318`; their rates are unruled,
`Q29`, `spec.md` §6.7 amendment). Remitting withheld tax to the authority (`PostingType.TaxRemittance`
exists; no story asks for it). A withholding certificate document: `spec.md` does not describe one.
VAT. **Every screen** — no Frontend story is cut, so by `D-117` no UI criterion is written.

## Questions for Karim

1. **`Q99`** — actual withheld amount versus the rate's figure, and the difference.
2. **`Q30`** — may the rate change after the first issued extract?
3. **The withholding base** and **whether the hold release carries withholding** — running default `RulingSet.WithholdingBase`. Registered as **`Q114`**; it is the same question as `KAFF-500` question 3, so one answer serves both.
4. **`Q97`** — Owner approval of a receipt.
