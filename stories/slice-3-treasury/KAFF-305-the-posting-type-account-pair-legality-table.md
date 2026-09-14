# KAFF-305 · The posting-type × account-pair legality table

<!-- kaff id=KAFF-305 slice=3 points=8 state=READY verdict=none at=- on=2026-09-14 -->

**Slice:** 3 (Treasury) · **Epic:** Treasury · **Points:** 8 (`stories/backlog.md` slice-3 table) · **Status:** NOT-BUILT.
**Spec:** §6.2 (posting types, cash and non-cash), §6.3 (account tree, "not an open-ended chart of accounts"), §6.4 (five ledgers) · **Decisions:** D-034 (تشوينات direction, the case study this rule exists to generalise)
**Register:** none of `Q14`/`Q15`/`Q16`/`Q29` gate this story. New: `Q91`
**Owner:** Backend
**Depends on:** `PostingType` (41 members) and `AccountType` (34 members) already exist as closed
enumerations [Verified: 2026-09-14 @ `src/Domain/Treasury/PostingType.cs`, `AccountType.cs`].
`kaff_postings_validate` already refuses several *structural* combinations (roll-up nodes, inactive
accounts, currency mismatch, ledger netting, hold debits, cross-project, closed period) but **does not
check which `PostingType` may run against which pair of `AccountType`s at all**
[Verified: 2026-09-14 @ `src/Infrastructure/Persistence/Sql/001_guards.sql` -> `kaff_postings_validate`
— no `NEW."type"` check against `v_from.type`/`v_to.type` exists anywhere in the function except the
single `Hold`/`HoldRelease` case].

## Why this story exists

D-034 is the case study this rule generalises: `MaterialAdvance` was modelled with the wrong sign, and
nothing in the domain or the database stopped a `MaterialAdvanceIssue` posting running in an
economically impossible direction — only a hand-walk of §15 caught it. Today, nothing stops a
`SupplierPayment` posting between a `Hold` account and a `ClientReceivable` account, or a
`HoldAccrual` posting between two `Bank` accounts. Every posting type names, in its own XML remark,
which accounts it is meant to move between (`PostingType.cs`'s comments), but that intent is prose, not
a rule the system enforces. This story turns those comments into an enforced table: which
`PostingType` may post between which pair of `AccountType`s (or, more precisely, which `AccountClass`/
`LedgerKind`/structural role), with everything else refused.

## Business rules

| # | Rule | Source |
|---|---|---|
| 1 | Every posting traces to a business event `spec.md` names — there is no `Manual` or `Other` posting type, and there must be no way to combine a real type with an account pair it was not meant for | `spec.md` §1 (general ledger with free-form entries out of scope) · [Verified: 2026-09-14 @ `PostingType.cs` header remark] |
| 2 | Postings into the Hold ledger accrue only via `HoldAccrual`; the only posting type permitted to move value **out** of Hold is `HoldRelease` | `spec.md` §5.1 · [Verified: 2026-09-14 @ `PostingType.cs` -> `HoldAccrual`, `HoldRelease` remarks; already partially enforced by `kaff_postings_validate`'s `KAFF_HOLD_DEBIT` check, which is type-aware only for this one ledger] |
| 3 | تشوينات moves only via `MaterialAdvanceIssue` (in) and `MaterialAdvanceRecovery` (out) | `spec.md` §5.1, §15 · D-034 · [Verified: 2026-09-14 @ `PostingType.cs` remarks] |
| 4 | Client advance moves only via `ClientAdvanceReceipt` (in) and `ClientAdvanceRecovery` (out) | `spec.md` §15 · [Verified: 2026-09-14 @ `PostingType.cs` remarks] |
| 5 | Firm advance moves only via `FirmAdvanceIssue` (in) and `FirmAdvanceRecovery` (out) | `spec.md` §6.4.3 · [Verified: 2026-09-14 @ `PostingType.cs` remarks] |
| 6 | Owner current account moves only via `OwnerInjection`, `OwnerWithdrawal`, `OwnerRepayment`, `OwnerDrawing` | `spec.md` §6.4.5 · [Verified: 2026-09-14 @ `PostingType.cs` remarks] |
| 7 | عهدة moves only via `PettyCashIssue`, `PettyCashSettlement`, `PettyCashReturn` | `spec.md` §6.4.4 · [Verified: 2026-09-14 @ `PostingType.cs` remarks] |
| 8 | A non-cash posting type (`RevenueRecognition`, `ExpenseAccrual`, `Depreciation`, `WipAdjustment`, `TaxWithheldAtSource`, `TaxWithholdingRetained`, etc.) must never touch a cash instrument (`Safe`, `Bank`) — that would make a non-cash event move cash, contradicting its own classification | `spec.md` §6.2 · [Verified: 2026-09-14 @ `PostingTypes.cs` -> `PostingNature.NonCash` list] |
| 9 | A reversal keeps the type of the posting it reverses (there is no `Reversal` type) and is exempt from the account-pair check only insofar as it mirrors an already-legal original — it does not get a second, looser check | [Verified: 2026-09-14 @ `PostingType.cs` header remark — "no `Reversal` type, because a reversal that lost its own classification would break every report that groups by type"] |
| 10 | The legality table is data reviewed against `spec.md`, not a rule a user can extend — adding a legal pair is a deliberate code change, same discipline as adding an `AccountType` member | `spec.md` §6.3 — "This is not an open-ended chart of accounts" |

## Permissions, money, audit, i18n

- **Permissions.** None of its own. This is a validation rule inside the posting pipeline `KAFF-301`
  and `KAFF-303` already gate by `TreasuryPostProject`/`TreasuryPostCompany`. No new endpoint.
- **Money.** No money moves; this story only narrows which movements the existing endpoints accept.
  A posting refused by this rule must be refused *before* the non-negative and ledger-netting checks
  run against real balances, so a request that will be refused anyway does not first take an advisory
  lock (performance note, not a business rule — Backend's call on ordering).
- **Audit.** None of its own — a refused posting writes no row (postings are the only audit trail here,
  and a rejected attempt creates none).
- **i18n.** New refusal needs a new `TreasuryErrors` entry and `errors.treasury.*` key —
  `errors.treasury.posting_type_account_mismatch` (or equivalent name Backend chooses) does not exist
  today [Verified: 2026-09-14 @ `en.json` lines 228-252 — no such key].

## Acceptance criteria

**AC-305-A — a legal pair is accepted**
Given `PostingType.ClientAdvanceReceipt` between a `Bank`/`Safe` account and a `ClientAdvance` account
When the posting is attempted
Then it succeeds
*Rule: 4.*

**AC-305-B — an illegal pair is refused, both from the domain and from the database**
Given `PostingType.SupplierPayment` attempted between a `Hold` account and a `ClientReceivable` account
When the posting is attempted
Then the domain refuses it with a translated message, and if the domain check is somehow bypassed the database refuses it too
*Rule: 1, 10. `TreasuryErrors`' own header remark: "If the two ever disagree, the database wins and the domain has a bug."*

**AC-305-C — only `HoldRelease` may debit the Hold ledger, and this is a type-aware check, not merely a ledger-aware one**
Given a posting attempting to move value out of a `Hold` account with a type other than `HoldRelease`
When it is attempted
Then it is refused — this restates `KAFF-301`'s `AC-301-E` but as a case of the general table rather than a one-off ledger check
*Rule: 2.*

**AC-305-D — تشوينات only moves via its two named types**
Given a posting of any type other than `MaterialAdvanceIssue` or `MaterialAdvanceRecovery` attempting to touch a `MaterialAdvance` account
When it is attempted
Then it is refused
*Rule: 3.*

**AC-305-E — a non-cash posting type is refused if either side is `Safe` or `Bank`**
Given `PostingType.Depreciation` (or any other `PostingNature.NonCash` type) attempted with `Safe` or `Bank` as either side
When it is attempted
Then it is refused
*Rule: 8.*

**AC-305-F — every defined `PostingType` has at least one legal pair, or the type is provably unreachable and flagged**
Given the full `PostingType` enumeration (41 members today)
When the legality table is built
Then a test enumerates every member and asserts it has at least one row, the same discipline `PostingTypes.NatureOf` already applies for cash/non-cash
*Rule: 1, 10. Mirrors `PostingTypes.cs`'s own remark: "Held as data rather than as a numeric range check so that adding a posting type forces an explicit decision."*

**AC-305-G — a reversal is checked against the table using its own (inherited) type, not exempted from it**
Given a reversal of a legal original posting
When the reversal is validated
Then it passes the same legality check its type would need to pass forward, using the swapped account pair
*Rule: 9.*

## Definition of Ready

| DoR item | |
|---|---|
| Every criterion is Given / When / Then | ✅ |
| Stable `AC-305-<LETTER>` ids | ✅ |
| Every business rule cites a `spec.md` section, a D-number, or a `[Verified: ...]` citation | ✅ |
| No uncited rule | ✅ |
| Permissions named explicitly | ✅ — none new |
| Money behaviour named explicitly | ✅ |
| Arabic UI strings as i18n keys | ⚠️ New key needed, named but not yet added to `en.json` |
| The audit record it writes is stated | ✅ — none |
| QA has written at least one scenario that fails if the rule is broken | ✅ **Met, 2026-09-14.** `qa/slice-3/test-cases.md` `TC-3-052`…`059` — `059` HELD on `Q91` for the unruled posting types |
| Story-currency citations dated with a stable identifier | ✅ |
| Not `BLOCKED` on an open question | ⚠️ **`AC-305-F`'s full table cannot be completed without Karim/Architect input on several posting types whose legal account pair is not stated anywhere in `spec.md` or in `PostingType.cs`'s own remarks** — see below |

**This story cannot reach full `READY` for every row of the table without the answer below.** The core
mechanism (rules 1–9, `AC-305-A` through `E` and `G`) can be built and verified against the pairs
`spec.md` states explicitly; the exhaustiveness test in `AC-305-F` is what will surface every pair that
is not yet stated anywhere.

## Not in this story

- **The posting and reversal endpoints themselves.** `KAFF-301`, `KAFF-303`.
- **Ledger-specific business rules beyond "which account pair is legal"** — the 20% hold rate, the
  75%/schedule of تشوينات recovery (`Q58`), the firm advance cap, عهدة's ceiling. `KAFF-306`–`312`.
- **Withholding posting pairs** (`TaxWithheldAtSource`, `TaxWithholdingRetained`) beyond confirming they
  never touch cash directly (rule 8) — the concrete party accounts they run against are `KAFF-317`/`318`.
- **Translating the database's own exception text for an illegal pair into the i18n message**, if the
  domain check is bypassed — cross-cutting with `KAFF-319`.

## Questions for Karim

| # | |
|---|---|
| **`Q91`** | **"For several posting types — `SiteExpensePayment`, `AssetPurchase`, `LoanDrawdown`, `PeriodCloseTransfer`, `YearEndProfitTransfer` among them — `spec.md` names the business event but not the exact pair of accounts it moves between. Is the account pair for each of these obvious enough to infer from the account tree, or does each need its own ruling the way تشوينات did?"** Say why it is being asked: D-034 is the entire reason this story exists — a plausible-looking direction was wrong and only a hand-walk of §15 caught it. `PostingType.cs`'s own XML remarks are prose written by whoever modelled the type, not a citation to a spec.md line for every member, and this story's exhaustiveness check (`AC-305-F`) will name every type that has no stated pair. **Not inferred here for any type beyond the nine rules already cited; the exhaustiveness test is what will produce the concrete list to bring back.** |
